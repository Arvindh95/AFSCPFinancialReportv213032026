using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FinancialReport.Services;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;

namespace FinancialReport
{
    /// <summary>
    /// Maintenance graph for GI Data Sources and their column definitions.
    ///
    /// A GI Data Source maps any Acumatica Generic Inquiry to a set of placeholder values
    /// for use in presentations and Word templates. Unlike FLRTReportDefinition (which is
    /// GL Trial Balance-specific), this graph imposes no assumptions about GI structure —
    /// the user configures column types, aggregation functions, and OData filter columns.
    /// </summary>
    public class FLRTGIDataSourceMaint : PXGraph<FLRTGIDataSourceMaint, FLRTGIDataSource>
    {
        #region Views

        public SelectFrom<FLRTGIDataSource>.View DataSource;

        public SelectFrom<FLRTGIDataSourceColumn>
            .Where<FLRTGIDataSourceColumn.dataSourceID.IsEqual<FLRTGIDataSource.dataSourceID.FromCurrent>>
            .OrderBy<FLRTGIDataSourceColumn.sortOrder.Asc>
            .View Columns;

        #endregion

        #region Standard Actions

        public new PXSave<FLRTGIDataSource>     Save;
        public new PXCancel<FLRTGIDataSource>   Cancel;
        public new PXInsert<FLRTGIDataSource>   Insert;
        public new PXDelete<FLRTGIDataSource>   Delete;
        public new PXFirst<FLRTGIDataSource>    First;
        public new PXPrevious<FLRTGIDataSource> Previous;
        public new PXNext<FLRTGIDataSource>     Next;
        public new PXLast<FLRTGIDataSource>     Last;

        #endregion

        #region Data Source Events

        protected void _(Events.RowSelected<FLRTGIDataSource> e)
        {
            if (e.Row == null) return;
            bool isNew = e.Cache.GetStatus(e.Row) == PXEntryStatus.Inserted;
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSource.dataSourceCD>(e.Cache, e.Row, isNew);
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSource.prefix>(e.Cache, e.Row, isNew);
            Actions["detectColumns"]?.SetEnabled(!string.IsNullOrWhiteSpace(e.Row.GIName));
        }

        protected void _(Events.RowPersisting<FLRTGIDataSource> e)
        {
            if (e.Row == null || e.Operation == PXDBOperation.Delete) return;

            if (string.IsNullOrWhiteSpace(e.Row.DataSourceCD))
            {
                e.Cache.RaiseExceptionHandling<FLRTGIDataSource.dataSourceCD>(
                    e.Row, e.Row.DataSourceCD,
                    new PXSetPropertyException("Data Source Code is required.", PXErrorLevel.Error, e.Row));
                return;
            }

            if (string.IsNullOrWhiteSpace(e.Row.Prefix))
            {
                e.Cache.RaiseExceptionHandling<FLRTGIDataSource.prefix>(
                    e.Row, e.Row.Prefix,
                    new PXSetPropertyException("Prefix is required.", PXErrorLevel.Error, e.Row));
                return;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(e.Row.Prefix, @"^[A-Za-z0-9]+$"))
            {
                e.Cache.RaiseExceptionHandling<FLRTGIDataSource.prefix>(
                    e.Row, e.Row.Prefix,
                    new PXSetPropertyException("Prefix must contain only letters and digits.", PXErrorLevel.Error, e.Row));
                return;
            }

            // Prefix uniqueness across all data sources
            FLRTGIDataSource duplicate = SelectFrom<FLRTGIDataSource>
                .Where<FLRTGIDataSource.prefix.IsEqual<@P.AsString>
                    .And<FLRTGIDataSource.dataSourceID.IsNotEqual<@P.AsInt>>>
                .View.Select(this, e.Row.Prefix, e.Row.DataSourceID ?? -1);

            if (duplicate != null)
            {
                e.Cache.RaiseExceptionHandling<FLRTGIDataSource.prefix>(
                    e.Row, e.Row.Prefix,
                    new PXSetPropertyException("Prefix must be unique across all GI Data Sources.", PXErrorLevel.Error, e.Row));
            }
        }

        #endregion

        #region Column Events

        protected void _(Events.RowSelected<FLRTGIDataSourceColumn> e)
        {
            if (e.Row == null) return;

            bool isValue      = e.Row.LineType == FLRTGIDataSourceColumn.ColumnLineType.Value;
            bool isCalculated = e.Row.LineType == FLRTGIDataSourceColumn.ColumnLineType.Calculated;
            bool isMultiRow   = e.Row.LineType == FLRTGIDataSourceColumn.ColumnLineType.MultiRow;
            bool isHeading    = e.Row.LineType == FLRTGIDataSourceColumn.ColumnLineType.Heading;

            // GIColumn: Value only
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSourceColumn.gIColumn>(e.Cache, e.Row, isValue);

            // Value-only fields
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSourceColumn.columnType>(e.Cache, e.Row, isValue);
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSourceColumn.aggregateFunction>(e.Cache, e.Row, isValue);

            // Shared: Value and MultiRow
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSourceColumn.keyFrom>(e.Cache, e.Row, isValue || isMultiRow);
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSourceColumn.keyTo>(e.Cache, e.Row, isValue || isMultiRow);
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSourceColumn.rowFilter>(e.Cache, e.Row, isValue || isMultiRow);

            // MultiRow-only fields
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSourceColumn.orderByColumn>(e.Cache, e.Row, isMultiRow);
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSourceColumn.orderByDirection>(e.Cache, e.Row, isMultiRow);
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSourceColumn.rowLimit>(e.Cache, e.Row, isMultiRow);
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSourceColumn.displayColumns>(e.Cache, e.Row, isMultiRow);

            // Calculated-only fields
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSourceColumn.formula>(e.Cache, e.Row, isCalculated);

            // Heading has no value
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSourceColumn.isVisible>(e.Cache, e.Row, !isHeading);
            PXUIFieldAttribute.SetEnabled<FLRTGIDataSourceColumn.formatString>(e.Cache, e.Row, !isHeading);
        }

        protected void _(Events.FieldUpdated<FLRTGIDataSourceColumn, FLRTGIDataSourceColumn.lineType> e)
        {
            if (e.Row == null) return;

            switch (e.Row.LineType)
            {
                case FLRTGIDataSourceColumn.ColumnLineType.Calculated:
                case FLRTGIDataSourceColumn.ColumnLineType.Heading:
                    e.Cache.SetValue<FLRTGIDataSourceColumn.gIColumn>(e.Row, null);
                    e.Cache.SetValue<FLRTGIDataSourceColumn.keyFrom>(e.Row, null);
                    e.Cache.SetValue<FLRTGIDataSourceColumn.keyTo>(e.Row, null);
                    e.Cache.SetValue<FLRTGIDataSourceColumn.rowFilter>(e.Row, null);
                    break;
                // MultiRow keeps GIColumn (used as display columns), KeyFrom, KeyTo, RowFilter
            }

            if (e.Row.LineType != FLRTGIDataSourceColumn.ColumnLineType.Calculated)
                e.Cache.SetValue<FLRTGIDataSourceColumn.formula>(e.Row, null);

            if (e.Row.LineType == FLRTGIDataSourceColumn.ColumnLineType.Heading)
                e.Cache.SetValue<FLRTGIDataSourceColumn.isVisible>(e.Row, false);
        }

        protected void _(Events.RowPersisting<FLRTGIDataSourceColumn> e)
        {
            if (e.Row == null || e.Operation == PXDBOperation.Delete) return;

            if (string.IsNullOrWhiteSpace(e.Row.ColumnAlias))
            {
                e.Cache.RaiseExceptionHandling<FLRTGIDataSourceColumn.columnAlias>(
                    e.Row, e.Row.ColumnAlias,
                    new PXSetPropertyException("Column Alias is required.", PXErrorLevel.Error, e.Row));
                return;
            }

            if (e.Row.LineType == FLRTGIDataSourceColumn.ColumnLineType.Value
                && string.IsNullOrWhiteSpace(e.Row.GIColumn))
            {
                e.Cache.RaiseExceptionHandling<FLRTGIDataSourceColumn.gIColumn>(
                    e.Row, e.Row.GIColumn,
                    new PXSetPropertyException("GI Column is required for Value lines.", PXErrorLevel.Error, e.Row));
            }

            if (e.Row.LineType == FLRTGIDataSourceColumn.ColumnLineType.Calculated
                && string.IsNullOrWhiteSpace(e.Row.Formula))
            {
                e.Cache.RaiseExceptionHandling<FLRTGIDataSourceColumn.formula>(
                    e.Row, e.Row.Formula,
                    new PXSetPropertyException("Formula is required for Calculated lines.", PXErrorLevel.Error, e.Row));
            }

            // ColumnAlias uniqueness within the same data source
            FLRTGIDataSourceColumn duplicate = SelectFrom<FLRTGIDataSourceColumn>
                .Where<FLRTGIDataSourceColumn.dataSourceID.IsEqual<@P.AsInt>
                    .And<FLRTGIDataSourceColumn.columnAlias.IsEqual<@P.AsString>>
                    .And<FLRTGIDataSourceColumn.columnID.IsNotEqual<@P.AsInt>>>
                .View.Select(this, e.Row.DataSourceID, e.Row.ColumnAlias, e.Row.ColumnID ?? -1);

            if (duplicate != null)
            {
                e.Cache.RaiseExceptionHandling<FLRTGIDataSourceColumn.columnAlias>(
                    e.Row, e.Row.ColumnAlias,
                    new PXSetPropertyException("Column Alias must be unique within the data source.", PXErrorLevel.Error, e.Row));
            }
        }

        #endregion

        #region Actions

        public PXAction<FLRTGIDataSource> DetectColumns;
        public PXAction<FLRTGIDataSource> TestFetch;

        /// <summary>
        /// Connects to the configured GI via OData, retrieves available column names,
        /// and logs them to trace. Helps the user discover what to map.
        /// </summary>
        [PXButton(CommitChanges = true)]
        [PXUIField(DisplayName = "Detect Columns")]
        public virtual IEnumerable detectColumns(PXAdapter adapter)
        {
            var ds = DataSource.Current;
            if (ds == null) return adapter.Get();

            if (string.IsNullOrWhiteSpace(ds.GIName))
                throw new PXException("Generic Inquiry name is required before detecting columns.");

            var credential = SelectFrom<FLRTTenantCredentials>.View.SelectSingleBound(this, null);
            if (credential == null)
                throw new PXException("No API credentials found. Configure tenant credentials first.");

            FLRTTenantCredentials cred = (FLRTTenantCredentials)credential;

            try
            {
                var authService = new AuthService(cred.BaseURL, cred.ClientIDNew, cred.ClientSecretNew, cred.UsernameNew, cred.PasswordNew);
                var dataService = new FinancialDataService(authService, cred.TenantName);
                List<string> columns = dataService.FetchGIColumns(ds.GIName);

                if (columns == null || columns.Count == 0)
                    throw new PXException($"No columns detected from GI '{ds.GIName}'. Verify the GI name and API credentials.");

                // Store OData column names so the selector dropdown shows them
                ds.DetectedColumns = string.Join(",", columns);
                DataSource.Update(ds);
                Actions.PressSave();

                PXTrace.WriteInformation($"[GIDataSource] Detected {columns.Count} columns from '{ds.GIName}': {string.Join(", ", columns)}");

                DataSource.Ask(
                    $"Detected {columns.Count} OData columns:\n{string.Join(", ", columns)}\n\nThese are now available in column dropdowns.",
                    MessageButtons.OK);
            }
            catch (PXException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PXException($"Failed to detect columns from GI '{ds.GIName}': {ex.Message}");
            }

            return adapter.Get();
        }

        /// <summary>
        /// Executes the GI Data Source configuration against the live GI, aggregates all VALUE
        /// columns, resolves CALCULATED formulas, and displays results in a dialog + trace log.
        /// Used for verifying correctness before wiring into presentations.
        /// </summary>
        [PXButton(CommitChanges = true)]
        [PXUIField(DisplayName = "Test Fetch")]
        public virtual IEnumerable testFetch(PXAdapter adapter)
        {
            var ds = DataSource.Current;
            if (ds == null) return adapter.Get();

            if (string.IsNullOrWhiteSpace(ds.GIName))
                throw new PXException("Generic Inquiry name is required before testing.");

            // Ask user for Year and Month via a simple dialog
            if (TestFilter.AskExt() != WebDialogResult.OK)
                return adapter.Get();

            var filter = TestFilter.Current;
            if (string.IsNullOrWhiteSpace(filter.TestYear))
                throw new PXException("Year is required for testing.");

            var credential = SelectFrom<FLRTTenantCredentials>.View.SelectSingleBound(this, null);
            if (credential == null)
                throw new PXException("No API credentials found. Configure tenant credentials first.");

            FLRTTenantCredentials cred = (FLRTTenantCredentials)credential;

            // Collect all column definitions
            var columnList = new List<FLRTGIDataSourceColumn>();
            foreach (FLRTGIDataSourceColumn col in Columns.Select())
                columnList.Add(col);

            if (columnList.Count == 0)
                throw new PXException("No columns defined. Add at least one column before testing.");

            try
            {
                var authService = new AuthService(cred.BaseURL, cred.ClientIDNew, cred.ClientSecretNew, cred.UsernameNew, cred.PasswordNew);
                var fetchService = new GIDataFetchService(authService, cred.BaseURL, cred.TenantName);

                Dictionary<string, string> results = fetchService.FetchAndAggregate(
                    ds, columnList,
                    filter.TestYear,
                    filter.TestMonth ?? "12",
                    filter.TestBranch,
                    filter.TestOrganization,
                    filter.TestLedger);

                // Build result display
                var sb = new StringBuilder();
                sb.AppendLine($"Test Results for {ds.DataSourceCD} (GI: {ds.GIName})");
                sb.AppendLine($"Period: {filter.TestYear}-{(filter.TestMonth ?? "12").PadLeft(2, '0')}");
                if (!string.IsNullOrWhiteSpace(filter.TestBranch))
                    sb.AppendLine($"Branch: {filter.TestBranch}");
                if (!string.IsNullOrWhiteSpace(filter.TestOrganization))
                    sb.AppendLine($"Organization: {filter.TestOrganization}");
                if (!string.IsNullOrWhiteSpace(filter.TestLedger))
                    sb.AppendLine($"Ledger: {filter.TestLedger}");
                sb.AppendLine();

                if (results.Count == 0)
                {
                    sb.AppendLine("No results produced. Check filter configuration and GI data.");
                }
                else
                {
                    // Find longest key for alignment
                    int maxKeyLen = results.Keys.Max(k => k.Length);

                    foreach (var kvp in results.OrderBy(k => k.Key))
                    {
                        string paddedKey = $"{{{{{kvp.Key}}}}}".PadRight(maxKeyLen + 4 + 2); // +4 for {{ }}, +2 padding
                        sb.AppendLine($"  {paddedKey} = {kvp.Value}");
                    }
                }

                string resultText = sb.ToString();
                PXTrace.WriteInformation($"[GIDataSource TestFetch]\n{resultText}");

                // Show in dialog (truncate if too long for dialog)
                string dialogText = resultText.Length > 2000
                    ? resultText.Substring(0, 2000) + "\n\n... (see trace log for full results)"
                    : resultText;

                DataSource.Ask(dialogText, MessageButtons.OK);
            }
            catch (PXException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PXException($"Test fetch failed: {ex.Message}");
            }

            return adapter.Get();
        }

        #endregion

        #region Test Filter

        public PXFilter<GITestFetchFilter> TestFilter;

        /// <summary>
        /// Non-persisted filter DAC for the Test Fetch dialog.
        /// </summary>
        [Serializable]
        [PXHidden]
        public class GITestFetchFilter : PXBqlTable, IBqlTable
        {
            #region TestYear
            [PXString(4, IsUnicode = true)]
            [PXDefault("2026", PersistingCheck = PXPersistingCheck.Nothing)]
            [PXUIField(DisplayName = "Year")]
            public virtual string TestYear { get; set; }
            public abstract class testYear : PX.Data.BQL.BqlString.Field<testYear> { }
            #endregion

            #region TestMonth
            [PXString(2, IsUnicode = true)]
            [PXDefault("12", PersistingCheck = PXPersistingCheck.Nothing)]
            [PXUIField(DisplayName = "Month")]
            [PXStringList(
                new string[] { "01","02","03","04","05","06","07","08","09","10","11","12" },
                new string[] { "01 - Jan","02 - Feb","03 - Mar","04 - Apr","05 - May","06 - Jun",
                               "07 - Jul","08 - Aug","09 - Sep","10 - Oct","11 - Nov","12 - Dec" })]
            public virtual string TestMonth { get; set; }
            public abstract class testMonth : PX.Data.BQL.BqlString.Field<testMonth> { }
            #endregion

            #region TestBranch
            [PXString(30, IsUnicode = true)]
            [PXUIField(DisplayName = "Branch")]
            public virtual string TestBranch { get; set; }
            public abstract class testBranch : PX.Data.BQL.BqlString.Field<testBranch> { }
            #endregion

            #region TestOrganization
            [PXString(50, IsUnicode = true)]
            [PXUIField(DisplayName = "Organization")]
            public virtual string TestOrganization { get; set; }
            public abstract class testOrganization : PX.Data.BQL.BqlString.Field<testOrganization> { }
            #endregion

            #region TestLedger
            [PXString(20, IsUnicode = true)]
            [PXUIField(DisplayName = "Ledger")]
            public virtual string TestLedger { get; set; }
            public abstract class testLedger : PX.Data.BQL.BqlString.Field<testLedger> { }
            #endregion
        }

        #endregion
    }
}
