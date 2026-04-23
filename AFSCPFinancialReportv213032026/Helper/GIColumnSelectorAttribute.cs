using System;
using System.Collections;
using System.Collections.Generic;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Data.Maintenance.GI;
using FinancialReport.Services;

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
    /// as a dropdown. Primary source: live OData metadata (actual runtime column names
    /// like 'Type', 'FinancialPeriod', 'BranchID'). Falls back to GIResult table
    /// (design-time ObjectName_Field names) when OData fetch fails.
    /// ValidateValue = false so users can still type values not in the list.
    /// </summary>
    public class GIColumnSelectorAttribute : PXCustomSelectorAttribute
    {
        // Static cache: giName -> (columns, fetchedAt). 5-minute TTL.
        private static readonly Dictionary<string, Tuple<List<string>, DateTime>> _cache
            = new Dictionary<string, Tuple<List<string>, DateTime>>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _cacheLock = new object();
        private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

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
            if (giName == null)
            {
                var dsCache = graph?.Caches[typeof(FLRTGIDataSource)];
                if (dsCache?.Current is FLRTGIDataSource ds)
                    giName = ds.GIName;
            }

            if (string.IsNullOrWhiteSpace(giName))
                yield break;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Try live OData fetch (returns real runtime column names)
            List<string> odataCols = TryGetODataColumns(graph, giName);
            if (odataCols != null && odataCols.Count > 0)
            {
                foreach (string col in odataCols)
                {
                    if (string.IsNullOrWhiteSpace(col)) continue;
                    if (!seen.Add(col)) continue;
                    yield return new FLRTGIColumnItem { ColumnName = col };
                }
                yield break;
            }

            // 2. Fallback: GIResult table (design-time names)
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
                    colName = r.Caption.Replace(" ", "");
                else if (!string.IsNullOrWhiteSpace(r.Field) && !r.Field.TrimStart().StartsWith("="))
                    colName = $"{r.ObjectName}_{r.Field}";
                else
                    continue;

                if (!seen.Add(colName)) continue;

                yield return new FLRTGIColumnItem { ColumnName = colName };
            }
        }

        /// <summary>
        /// Fetches live OData column names via stored tenant credentials.
        /// Returns null on any failure (caller falls back to GIResult).
        /// Results cached 5 minutes per GI name.
        /// </summary>
        private static List<string> TryGetODataColumns(PXGraph graph, string giName)
        {
            try
            {
                lock (_cacheLock)
                {
                    Tuple<List<string>, DateTime> hit;
                    if (_cache.TryGetValue(giName, out hit) &&
                        DateTime.UtcNow - hit.Item2 < _cacheTtl)
                    {
                        return hit.Item1;
                    }
                }

                if (graph == null) return null;

                var credView = SelectFrom<FLRTTenantCredentials>.View.SelectSingleBound(graph, null);
                if (credView == null) return null;
                FLRTTenantCredentials cred = (FLRTTenantCredentials)credView;
                if (cred == null) return null;
                if (string.IsNullOrWhiteSpace(cred.BaseURL) || string.IsNullOrWhiteSpace(cred.TenantName))
                    return null;

                var authService = new AuthService(cred.BaseURL, cred.ClientIDNew, cred.ClientSecretNew, cred.UsernameNew, cred.PasswordNew);
                var dataService = new FinancialDataService(authService, cred.TenantName);
                List<string> cols = dataService.FetchGIColumns(giName);

                lock (_cacheLock)
                {
                    _cache[giName] = Tuple.Create(cols ?? new List<string>(), DateTime.UtcNow);
                }
                return cols;
            }
            catch (Exception ex)
            {
                PXTrace.WriteWarning($"[GIColumnSelector] OData fetch failed for '{giName}': {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Selector for GI Data Source screens. Prefers OData column names from DetectedColumns
    /// (populated by Detect Columns action) over the GIResult table fallback, because OData
    /// property names often differ from the internal ObjectName_Field format.
    /// </summary>
    public class GIDataSourceColumnSelectorAttribute : PXCustomSelectorAttribute
    {
        public GIDataSourceColumnSelectorAttribute()
            : base(typeof(FLRTGIColumnItem.columnName))
        {
            DescriptionField = typeof(FLRTGIColumnItem.columnName);
            ValidateValue    = false;
        }

        public IEnumerable GetRecords()
        {
            PXGraph graph = PXView.CurrentGraph ?? _Graph;

            // Try to read stored OData column names from DetectedColumns
            var dsCache = graph?.Caches[typeof(FLRTGIDataSource)];
            var ds = dsCache?.Current as FLRTGIDataSource;
            if (ds != null && !string.IsNullOrWhiteSpace(ds.DetectedColumns))
            {
                foreach (string col in ds.DetectedColumns.Split(','))
                {
                    string trimmed = col.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        yield return new FLRTGIColumnItem { ColumnName = trimmed };
                }
                yield break;
            }

            // Fallback: read from GIResult table (ObjectName_Field format)
            string giName = ds?.GIName;
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
                    colName = r.Caption.Replace(" ", "");
                else if (!string.IsNullOrWhiteSpace(r.Field) && !r.Field.TrimStart().StartsWith("="))
                    colName = $"{r.ObjectName}_{r.Field}";
                else
                    continue;

                if (!seen.Add(colName)) continue;
                yield return new FLRTGIColumnItem { ColumnName = colName };
            }
        }
    }
}