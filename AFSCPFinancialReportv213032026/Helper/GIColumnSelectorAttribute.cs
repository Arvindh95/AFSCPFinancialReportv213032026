using System;
using System.Collections;
using System.Collections.Generic;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Data.Maintenance.GI;

namespace FinancialReport.Helper
{
    /// <summary>
    /// Virtual projection DAC — return type for the GI column selector.
    /// Not mapped to any real database table.
    /// </summary>
    [Serializable]
    [PXVirtual]
    [PXHidden]
    public class FLRTGIColumnItem : PXBqlTable, IBqlTable
    {
        [PXString(500, IsKey = true)]
        [PXUIField(DisplayName = "Column Name")]
        public virtual string ColumnName { get; set; }
        public abstract class columnName : BqlString.Field<columnName> { }
    }

    /// <summary>
    /// Minimal read-only stub mapping to Acumatica's GIResult table (GI column definitions).
    /// Only used for BQL queries inside GIColumnSelectorAttribute — never written to.
    /// Schema migration is safe: all columns here already exist in the GIResult table.
    /// </summary>
    [Serializable]
    [PXHidden]
    public class GIResult : PXBqlTable, IBqlTable
    {
        [PXDBGuid(IsKey = true)]
        public virtual Guid? DesignID { get; set; }
        public abstract class designID : BqlGuid.Field<designID> { }

        [PXDBInt(IsKey = true)]
        public virtual int? LineNbr { get; set; }
        public abstract class lineNbr : BqlInt.Field<lineNbr> { }

        [PXDBString(255, IsUnicode = true)]
        public virtual string ObjectName { get; set; }
        public abstract class objectName : BqlString.Field<objectName> { }

        // nvarchar(MAX) in DB — use a large safe length; schema migration never shrinks existing columns
        [PXDBString(4000, IsUnicode = true)]
        public virtual string Field { get; set; }
        public abstract class field : BqlString.Field<field> { }

        [PXDBString(128, IsUnicode = true)]
        public virtual string Caption { get; set; }
        public abstract class caption : BqlString.Field<caption> { }

        [PXDBBool]
        public virtual bool? IsVisible { get; set; }
        public abstract class isVisible : BqlBool.Field<isVisible> { }
    }

    /// <summary>
    /// Selector that presents the GI columns for the currently selected Generic Inquiry
    /// as a dropdown. Column name = Caption (spaces stripped) when set, else ObjectName_Field.
    /// ValidateValue = false so users can still type values not in the list.
    /// </summary>
    public class GIColumnSelectorAttribute : PXCustomSelectorAttribute
    {
        public GIColumnSelectorAttribute()
            : base(typeof(FLRTGIColumnItem.columnName))
        {
            DescriptionField = typeof(FLRTGIColumnItem.columnName);
            ValidateValue    = false;
        }

        public IEnumerable GetRecords()
        {
            PXGraph graph = PXView.CurrentGraph ?? _Graph;

            string giName = null;
            var cache = graph?.Caches[typeof(FLRTReportDefinition)];
            if (cache?.Current is FLRTReportDefinition def)
                giName = def.GIName;

            if (string.IsNullOrWhiteSpace(giName))
                yield break;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rows = SelectFrom<GIResult>
                .InnerJoin<GIDesign>.On<GIResult.designID.IsEqual<GIDesign.designID>>
                .Where<GIDesign.name.IsEqual<@P.AsString>
                    .And<GIResult.isVisible.IsEqual<True>>>
                .OrderBy<GIResult.lineNbr.Asc>
                .View.Select(graph, giName);

            foreach (PXResult<GIResult, GIDesign> row in rows)
            {
                GIResult r = row;

                string colName;
                if (!string.IsNullOrWhiteSpace(r.Caption))
                {
                    // Named column (formula or captioned field) — strip spaces
                    colName = r.Caption.Replace(" ", "");
                }
                else if (!string.IsNullOrWhiteSpace(r.Field) && !r.Field.TrimStart().StartsWith("="))
                {
                    // Simple field without caption — use ObjectName_fieldName
                    colName = $"{r.ObjectName}_{r.Field}";
                }
                else
                {
                    // Formula field without a caption — skip (no usable column name)
                    continue;
                }

                if (!seen.Add(colName)) continue;

                yield return new FLRTGIColumnItem { ColumnName = colName };
            }
        }
    }
}