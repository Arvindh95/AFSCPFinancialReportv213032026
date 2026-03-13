using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FinancialReport.Helper;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;

namespace FinancialReport.Services
{
    /// <summary>
    /// The core financial report calculation engine.
    ///
    /// Supports both single-definition (legacy) and multi-definition (new) modes.
    ///
    /// Multi-definition mode (CalculateAll):
    ///   - Accepts any number of Report Definitions, each with a short prefix (e.g. "BS", "PL", "CF")
    ///   - Loads all line items from all definitions into a single processing graph
    ///   - Resolves cross-definition formula references (e.g. CF formula: BS_RETAINED_ASSETS - DISPOSED_ASSETS)
    ///   - Uses topological sort (Kahn's algorithm) to determine the correct calculation order automatically
    ///   - Detects circular dependencies and surfaces a clear error
    ///   - Produces prefixed placeholder keys: PREFIX_LINECODE_CY / PREFIX_LINECODE_PY
    ///
    /// Formula token resolution:
    ///   - Explicit:  "BS_TOTAL_ASSETS" → token starts with known prefix "BS_" → cross-definition reference
    ///   - Implicit:  "TOTAL_ASSETS"    → no prefix match → own-definition, resolved as "CURRENTPREFIX_TOTAL_ASSETS"
    ///
    /// The engine is stateless — create a new instance per report generation.
    /// </summary>
    public class ReportCalculationEngine
    {
        private readonly PXGraph _graph;
        private readonly RoundingSettings _rounding;

        // Global dictionaries used during CalculateAll — keyed by PREFIX_LINECODE (uppercase)
        private readonly Dictionary<string, decimal> _cyGlobal = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, decimal> _pyGlobal = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, decimal> _pmGlobal = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        // Compiled once at class load — reused across all formula evaluations
        private static readonly Regex FormulaTokenRegex = new Regex(
            @"([A-Z][A-Z0-9_]*)|([\d]+\.?[\d]*)|([\+\-\*\/\(\)])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public ReportCalculationEngine(PXGraph graph, RoundingSettings rounding = null)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _rounding = rounding ?? new RoundingSettings();
        }

        // ─────────────────────────────────────────────────────────────────
        // PUBLIC STRUCTS & CLASSES
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Describes one Report Definition to be included in a multi-definition calculation.
        /// </summary>
        public struct DefinitionLink
        {
            /// <summary>The DB identity of the FLRTReportDefinition record.</summary>
            public int DefinitionID { get; set; }

            /// <summary>
            /// Short alphanumeric prefix (e.g. "BS", "PL", "CF").
            /// Namespaces all placeholder keys produced by this definition.
            /// Prefix + "_" + LineCode + "_CY" = the Word template placeholder key.
            /// </summary>
            public string Prefix { get; set; }

            /// <summary>Per-definition rounding configuration for value formatting.</summary>
            public RoundingSettings Rounding { get; set; }
        }

        // ─────────────────────────────────────────────────────────────────
        // INTERNAL NODE
        // ─────────────────────────────────────────────────────────────────

        private class LineNode
        {
            public FLRTReportLineItem Line     { get; set; }
            public string            Prefix   { get; set; }
            public int               DefinitionID { get; set; }
            public RoundingSettings  Rounding { get; set; }

            /// <summary>Global dictionary key: PREFIX_LINECODE (uppercase).</summary>
            public string GlobalKey => $"{Prefix}_{Line.LineCode}".ToUpper();
        }

        // ─────────────────────────────────────────────────────────────────
        // MULTI-DEFINITION ENTRY POINT
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calculates all line values across multiple linked definitions in one pass.
        /// Uses topological sort to determine calculation order automatically —
        /// cross-definition references are resolved without any manual sequencing.
        /// </summary>
        /// <param name="definitionLinks">All definitions linked to this report.</param>
        /// <param name="cyData">GL data for the current year period (shared across all definitions).</param>
        /// <param name="pyData">GL data for the prior year period (shared across all definitions).</param>
        /// <param name="cyOpeningData">
        /// GL data for the end of the year BEFORE the current year — i.e. the fiscal-year opening balance
        /// for CY lines with BalanceType=Beginning.  Pass prevYearData here.
        /// When null, falls back to cyData.BeginningBalance (legacy behaviour).
        /// </param>
        /// <param name="pyOpeningData">
        /// GL data for the end of the year BEFORE the prior year — i.e. the fiscal-year opening balance
        /// for PY lines with BalanceType=Beginning.  Pass prevYearPriorData here.
        /// When null, falls back to pyData.BeginningBalance (legacy behaviour).
        /// </param>
        /// <param name="cyJanOpeningData">
        /// GL data fetched from period 01-{currYear} via FetchJanuaryBeginningBalance.
        /// Used for BalanceType=JanuaryBeginning on CY lines (.BeginningBalance per account).
        /// </param>
        /// <param name="pyJanOpeningData">
        /// GL data fetched from period 01-{prevYear} via FetchJanuaryBeginningBalance.
        /// Used for BalanceType=JanuaryBeginning on PY lines (.BeginningBalance per account).
        /// </param>
        /// <param name="cyCumulativeData">
        /// Year-to-date range data for CY (e.g. Jan–Dec of current year).
        /// Used for BalanceType=Debit/Credit/Movement so the full-year totals are used,
        /// not just the single year-end period's movement values.
        /// </param>
        /// <param name="pyCumulativeData">
        /// Year-to-date range data for PY (e.g. Jan–Dec of prior year).
        /// Used for BalanceType=Debit/Credit/Movement on PY lines.
        /// </param>
        /// <param name="pmData">
        /// Single-period data for the month immediately before the selected financial month.
        /// Produces PREFIX_LINECODE_PM placeholder keys for month-over-month comparisons.
        /// All balance types use this single period (no cumulative for PM).
        /// </param>
        /// <returns>
        /// Unified placeholder dictionary keyed as PREFIX_LINECODE_CY / PREFIX_LINECODE_PY / PREFIX_LINECODE_PM.
        /// E.g. "BS_TOTAL_ASSETS_CY", "PL_NET_INCOME_PY", "KL_CASH_PM"
        /// </returns>
        public Dictionary<string, string> CalculateAll(
            IEnumerable<DefinitionLink> definitionLinks,
            FinancialApiData cyData,
            FinancialApiData pyData,
            FinancialApiData cyOpeningData = null,
            FinancialApiData pyOpeningData = null,
            FinancialApiData cyJanOpeningData = null,
            FinancialApiData pyJanOpeningData = null,
            FinancialApiData cyCumulativeData = null,
            FinancialApiData pyCumulativeData = null,
            FinancialApiData pmData = null)
        {
            _cyGlobal.Clear();
            _pyGlobal.Clear();
            _pmGlobal.Clear();

            var linkList = definitionLinks?.ToList();
            if (linkList == null || !linkList.Any())
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Collect known prefixes for formula token resolution
            var knownPrefixes = new HashSet<string>(
                linkList.Select(l => l.Prefix?.ToUpper()).Where(p => !string.IsNullOrEmpty(p)),
                StringComparer.OrdinalIgnoreCase);

            PXTrace.WriteInformation($"ReportCalculationEngine.CalculateAll: {linkList.Count} definition(s), prefixes: [{string.Join(", ", knownPrefixes)}]");

            // 1. Load all line items from all definitions
            var allNodes = LoadAllLineItems(linkList);
            if (!allNodes.Any())
            {
                PXTrace.WriteWarning("ReportCalculationEngine.CalculateAll: No line items found across all definitions.");
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            PXTrace.WriteInformation($"ReportCalculationEngine.CalculateAll: {allNodes.Count} total line items loaded.");

            // 2. Build parent→children map once — shared by BuildAndSort (for deps) and CalculateSubtotal (for summing).
            // Key: "{DefinitionID}_{ParentLineCode_uppercase}" → child LineNodes
            var childrenByParent = BuildChildrenMap(allNodes);

            // 3. Build dependency graph and topologically sort
            var sortedNodes = BuildAndSort(allNodes, knownPrefixes, childrenByParent);

            // 4. Process nodes in dependency-resolved order
            foreach (var node in sortedNodes)
            {
                if (string.IsNullOrWhiteSpace(node.Line.LineCode)) continue;
                if (node.Line.LineType == FLRTReportLineItem.LineItemType.Heading) continue;

                decimal cyVal = 0m;
                decimal pyVal = 0m;
                decimal pmVal = 0m;

                switch (node.Line.LineType)
                {
                    case FLRTReportLineItem.LineItemType.Account:
                        cyVal = CalculateAccountLine(node.Line, cyData, cyOpeningData, cyJanOpeningData, cyCumulativeData);
                        pyVal = CalculateAccountLine(node.Line, pyData, pyOpeningData, pyJanOpeningData, pyCumulativeData);
                        // PM: single-period previous month — no opening data, no cumulative
                        pmVal = CalculateAccountLine(node.Line, pmData);
                        break;

                    case FLRTReportLineItem.LineItemType.Subtotal:
                        cyVal = CalculateSubtotal(node.Line.LineCode, node.DefinitionID, node.Prefix, _cyGlobal, childrenByParent);
                        pyVal = CalculateSubtotal(node.Line.LineCode, node.DefinitionID, node.Prefix, _pyGlobal, childrenByParent);
                        pmVal = CalculateSubtotal(node.Line.LineCode, node.DefinitionID, node.Prefix, _pmGlobal, childrenByParent);
                        break;

                    case FLRTReportLineItem.LineItemType.Calculated:
                        cyVal = EvaluateFormula(node.Line.Formula, node.Prefix, knownPrefixes, _cyGlobal);
                        pyVal = EvaluateFormula(node.Line.Formula, node.Prefix, knownPrefixes, _pyGlobal);
                        pmVal = EvaluateFormula(node.Line.Formula, node.Prefix, knownPrefixes, _pmGlobal);
                        break;
                }

                _cyGlobal[node.GlobalKey] = cyVal;
                _pyGlobal[node.GlobalKey] = pyVal;
                _pmGlobal[node.GlobalKey] = pmVal;
            }

            return BuildPlaceholderMap(allNodes);
        }

        // ─────────────────────────────────────────────────────────────────
        // LEGACY SINGLE-DEFINITION ENTRY POINT (backward compatibility)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Legacy single-definition entry point. Internally delegates to CalculateAll.
        /// Output placeholder keys are now PREFIX_LINECODE_CY (e.g. BS_TOTAL_ASSETS_CY)
        /// rather than LINECODE_CY. Existing Word templates must be updated accordingly.
        /// </summary>
        public Dictionary<string, string> Calculate(
            int definitionID,
            FinancialApiData cyData,
            FinancialApiData pyData)
        {
            var def = SelectFrom<FLRTReportDefinition>
                .Where<FLRTReportDefinition.definitionID.IsEqual<@P.AsInt>>
                .View.Select(_graph, definitionID)
                .TopFirst;

            if (def == null)
            {
                PXTrace.WriteWarning($"ReportCalculationEngine.Calculate: Definition {definitionID} not found.");
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            // Fallback prefix if not set: first 10 chars of DefinitionCD
            string prefix = !string.IsNullOrWhiteSpace(def.DefinitionPrefix)
                ? def.DefinitionPrefix
                : def.DefinitionCD?.Length > 10
                    ? def.DefinitionCD.Substring(0, 10).ToUpper()
                    : def.DefinitionCD?.ToUpper() ?? "DEF";

            var link = new DefinitionLink
            {
                DefinitionID = definitionID,
                Prefix       = prefix,
                Rounding     = RoundingSettings.FromDefinition(def)
            };

            return CalculateAll(new[] { link }, cyData, pyData, cyOpeningData: null, pyOpeningData: null);
        }

        // ─────────────────────────────────────────────────────────────────
        // LINE ITEM LOADING
        // ─────────────────────────────────────────────────────────────────

        private List<LineNode> LoadAllLineItems(List<DefinitionLink> links)
        {
            var allNodes = new List<LineNode>();

            foreach (var link in links)
            {
                if (string.IsNullOrWhiteSpace(link.Prefix))
                {
                    PXTrace.WriteWarning($"ReportCalculationEngine: DefinitionID {link.DefinitionID} has no prefix — skipping.");
                    continue;
                }

                var lineItems = SelectFrom<FLRTReportLineItem>
                    .Where<FLRTReportLineItem.definitionID.IsEqual<@P.AsInt>>
                    .OrderBy<FLRTReportLineItem.sortOrder.Asc>
                    .View.Select(_graph, link.DefinitionID)
                    .RowCast<FLRTReportLineItem>()
                    .ToList();

                foreach (var line in lineItems)
                {
                    allNodes.Add(new LineNode
                    {
                        Line         = line,
                        Prefix       = link.Prefix.ToUpper(),
                        DefinitionID = link.DefinitionID,
                        Rounding     = link.Rounding ?? new RoundingSettings()
                    });
                }

                PXTrace.WriteInformation($"  Loaded {lineItems.Count} lines for definition {link.DefinitionID} (prefix: {link.Prefix})");
            }

            return allNodes;
        }

        // ─────────────────────────────────────────────────────────────────
        // TOPOLOGICAL SORT (KAHN'S ALGORITHM)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the parent→children lookup once — shared between BuildAndSort (dep resolution)
        /// and CalculateSubtotal (value summation), eliminating all per-subtotal DB queries.
        /// Key: "{DefinitionID}_{ParentLineCode_uppercase}"
        /// </summary>
        private static Dictionary<string, List<LineNode>> BuildChildrenMap(List<LineNode> allNodes)
        {
            var map = new Dictionary<string, List<LineNode>>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in allNodes)
            {
                if (string.IsNullOrWhiteSpace(n.Line.ParentLineCode)) continue;
                string key = $"{n.DefinitionID}_{n.Line.ParentLineCode.ToUpper()}";
                if (!map.TryGetValue(key, out var children))
                {
                    children = new List<LineNode>();
                    map[key] = children;
                }
                children.Add(n);
            }
            return map;
        }

        private List<LineNode> BuildAndSort(List<LineNode> allNodes, HashSet<string> knownPrefixes, Dictionary<string, List<LineNode>> childrenByParent)
        {
            // Map GlobalKey → node for fast lookup
            var validNodes = allNodes.Where(n => !string.IsNullOrWhiteSpace(n.Line.LineCode)).ToList();

            // Detect duplicate GlobalKeys before building the dictionary to produce a helpful error
            var duplicateKeys = validNodes
                .GroupBy(n => n.GlobalKey, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateKeys.Any())
                throw new PXException(Messages.DuplicateGlobalKey, string.Join(", ", duplicateKeys));

            var nodeMap = validNodes.ToDictionary(n => n.GlobalKey, n => n, StringComparer.OrdinalIgnoreCase);

            // Build dependency sets: deps[key] = set of keys that key depends on
            var deps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var node in nodeMap.Values)
            {
                string key = node.GlobalKey;
                deps[key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                switch (node.Line.LineType)
                {
                    case FLRTReportLineItem.LineItemType.Account:
                    case FLRTReportLineItem.LineItemType.Heading:
                        // No calculation dependencies
                        break;

                    case FLRTReportLineItem.LineItemType.Subtotal:
                        // Depends on all child lines — resolved from in-memory map (no DB query)
                        string parentLookupKey = $"{node.DefinitionID}_{node.Line.LineCode.ToUpper()}";
                        if (childrenByParent.TryGetValue(parentLookupKey, out var childNodes))
                        {
                            foreach (var child in childNodes)
                            {
                                if (!string.IsNullOrWhiteSpace(child.Line.LineCode))
                                    deps[key].Add(child.GlobalKey);
                            }
                        }
                        break;

                    case FLRTReportLineItem.LineItemType.Calculated:
                        // Depends on all tokens resolved from the formula
                        if (!string.IsNullOrWhiteSpace(node.Line.Formula))
                        {
                            var formulaDeps = ExtractFormulaDependencies(
                                node.Line.Formula, node.Prefix, knownPrefixes);
                            foreach (var dep in formulaDeps)
                                deps[key].Add(dep);
                        }
                        break;
                }
            }

            // Kahn's algorithm
            // inDegree[key]  = number of unresolved dependencies for this node
            // dependents[key] = reverse edges: nodes that become unblocked when key is processed
            var inDegree   = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // Build inDegree and reverse-edge (dependents) map in one pass
            foreach (var kvp in deps)
            {
                string key = kvp.Key;
                int count = 0;

                foreach (string dep in kvp.Value)
                {
                    if (!nodeMap.ContainsKey(dep)) continue; // ignore refs to unknown nodes

                    count++;

                    if (!dependents.ContainsKey(dep))
                        dependents[dep] = new List<string>();
                    dependents[dep].Add(key);
                }

                inDegree[key] = count;
            }

            var queue = new Queue<string>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                    queue.Enqueue(kvp.Key);
            }

            var sortedKeys = new List<string>();
            while (queue.Count > 0)
            {
                string key = queue.Dequeue();
                sortedKeys.Add(key);

                if (dependents.TryGetValue(key, out var depList))
                {
                    foreach (string dependent in depList)
                    {
                        inDegree[dependent]--;
                        if (inDegree[dependent] == 0)
                            queue.Enqueue(dependent);
                    }
                }
            }

            // Cycle detection
            if (sortedKeys.Count < nodeMap.Count)
            {
                var cycleNodes = inDegree
                    .Where(kv => kv.Value > 0)
                    .Select(kv => kv.Key)
                    .ToList();

                throw new PXException(
                    Messages.CircularDependencyDetected,
                    string.Join(", ", cycleNodes));
            }

            // Return nodes in resolved order; nodes not in deps (HEADINGs, invalid) go last
            var sortedNodeList = sortedKeys
                .Where(k => nodeMap.ContainsKey(k))
                .Select(k => nodeMap[k])
                .ToList();

            // Append any nodes that weren't in the dep graph (HEADINGs etc.)
            var sortedSet = new HashSet<string>(sortedKeys, StringComparer.OrdinalIgnoreCase);
            foreach (var node in allNodes)
            {
                if (!string.IsNullOrWhiteSpace(node.Line.LineCode) && !sortedSet.Contains(node.GlobalKey))
                    sortedNodeList.Add(node);
            }

            return sortedNodeList;
        }

        // ─────────────────────────────────────────────────────────────────
        // FORMULA DEPENDENCY EXTRACTION
        // ─────────────────────────────────────────────────────────────────

        private HashSet<string> ExtractFormulaDependencies(
            string formula, string currentPrefix, HashSet<string> knownPrefixes)
        {
            var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tokens = TokenizeFormula(formula);

            foreach (string token in tokens)
            {
                if (decimal.TryParse(token, out _)) continue;
                if ("+-*/()".Contains(token)) continue;
                if (string.IsNullOrWhiteSpace(token)) continue;

                string resolved = ResolveToken(token, currentPrefix, knownPrefixes);
                deps.Add(resolved.ToUpper());
            }

            return deps;
        }

        // ─────────────────────────────────────────────────────────────────
        // TOKEN RESOLUTION (explicit vs. implicit prefix)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves a formula token to its global dictionary key (PREFIX_LINECODE).
        ///
        /// Explicit: token starts with a known prefix + "_"
        ///   → "BS_TOTAL_ASSETS" in any formula → resolved as "BS_TOTAL_ASSETS"
        ///
        /// Implicit: no known prefix detected
        ///   → "TOTAL_ASSETS" in a CF formula → resolved as "CF_TOTAL_ASSETS"
        /// </summary>
        private string ResolveToken(string token, string currentPrefix, HashSet<string> knownPrefixes)
        {
            // Sort longest prefix first to avoid shorter prefix shadowing a longer one (e.g. PL vs PLS).
            foreach (string prefix in knownPrefixes.OrderByDescending(p => p.Length))
            {
                if (token.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase))
                    return token.ToUpper(); // already fully qualified
            }

            // Implicit: belongs to own definition
            return $"{currentPrefix}_{token}".ToUpper();
        }

        // ─────────────────────────────────────────────────────────────────
        // ACCOUNT LINE CALCULATION
        // ─────────────────────────────────────────────────────────────────

        private decimal CalculateAccountLine(FLRTReportLineItem line, FinancialApiData data, FinancialApiData openingData = null, FinancialApiData janOpeningData = null, FinancialApiData cumulativeData = null)
        {
            if (data == null) return 0m;
            if (string.IsNullOrWhiteSpace(line.AccountFrom) || string.IsNullOrWhiteSpace(line.AccountTo))
                return 0m;

            bool hasFilter = !string.IsNullOrWhiteSpace(line.SubaccountFilter)
                          || !string.IsNullOrWhiteSpace(line.BranchFilter)
                          || !string.IsNullOrWhiteSpace(line.OrganizationFilter)
                          || !string.IsNullOrWhiteSpace(line.LedgerFilter);

            if (hasFilter && data.DetailRows != null && data.DetailRows.Count > 0)
                return CalculateAccountLineFromDetail(line, data.DetailRows, openingData?.DetailRows, janOpeningData?.DetailRows, cumulativeData?.DetailRows);

            bool isBeginning        = string.Equals(line.BalanceType, FLRTReportLineItem.BalanceTypeValue.Beginning,        StringComparison.OrdinalIgnoreCase);
            bool isJanuaryBeginning = string.Equals(line.BalanceType, FLRTReportLineItem.BalanceTypeValue.JanuaryBeginning, StringComparison.OrdinalIgnoreCase);
            bool isDebit    = string.Equals(line.BalanceType, FLRTReportLineItem.BalanceTypeValue.Debit,    StringComparison.OrdinalIgnoreCase);
            bool isCredit   = string.Equals(line.BalanceType, FLRTReportLineItem.BalanceTypeValue.Credit,   StringComparison.OrdinalIgnoreCase);
            bool isMovement = string.Equals(line.BalanceType, FLRTReportLineItem.BalanceTypeValue.Movement, StringComparison.OrdinalIgnoreCase);

            // For Debit/Credit/Movement use the year-to-date cumulative data set (Jan–Dec range)
            // so that transactions from any month are captured, not just the year-end period's values.
            bool useCumulative = (isDebit || isCredit || isMovement) && cumulativeData?.AccountData != null;
            var sourceData = useCumulative ? cumulativeData : data;

            if (sourceData == null || sourceData.AccountData == null) return 0m;

            decimal total = 0m;
            int matched = 0;

            foreach (var kvp in sourceData.AccountData)
            {
                string accountCode = kvp.Key;
                FinancialPeriodData periodData = kvp.Value;

                if (!IsAccountInRange(accountCode, line.AccountFrom, line.AccountTo))
                    continue;

                if (!string.IsNullOrWhiteSpace(line.AccountTypeFilter)
                    && !string.Equals(periodData.AccountType, line.AccountTypeFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                decimal rawValue;
                if (isBeginning && openingData?.AccountData != null)
                {
                    // Opening balance = EndingBalance of the prior fiscal-year-end period.
                    // Works for any fiscal year end month.
                    rawValue = openingData.AccountData.TryGetValue(accountCode, out FinancialPeriodData openingPeriod)
                        ? openingPeriod.EndingBalance
                        : 0m;
                }
                else if (isJanuaryBeginning && janOpeningData?.AccountData != null)
                {
                    // Opening balance = BeginningBalance of period 01-{Year}.
                    // Best suited for calendar-year (Jan–Dec) fiscal periods.
                    rawValue = janOpeningData.AccountData.TryGetValue(accountCode, out FinancialPeriodData janPeriod)
                        ? janPeriod.BeginningBalance
                        : 0m;
                }
                else
                {
                    // For Debit/Credit/Movement, periodData is already from cumulativeData (full-year).
                    // For Ending/Beginning fallback, periodData is from the single year-end period.
                    rawValue = GetBalanceByType(periodData, line.BalanceType);
                }

                decimal signCorrected = ApplyAccountTypeSign(rawValue, periodData.AccountType);
                decimal finalValue  = line.SignRule == FLRTReportLineItem.SignRuleValue.Flip
                    ? signCorrected * -1
                    : signCorrected;

                total += finalValue;
                matched++;
            }

            return total;
        }

        /// <summary>Builds a dimension composite key for detail-row index lookups.</summary>
        private static string DetailKey(string account, string subaccount, string branchID, string organizationID, string ledger)
            => $"{account}|{subaccount}|{branchID}|{organizationID}|{ledger}";

        /// <summary>
        /// Builds an O(1)-lookup dictionary from a detail rows list, keyed by
        /// "Account|Subaccount|BranchID|OrganizationID|Ledger". Last row wins on collision (no exact dupes expected).
        /// Returns null when the source list is null or empty.
        /// </summary>
        private static Dictionary<string, FinancialPeriodData> BuildDetailIndex(List<FinancialPeriodData> rows)
        {
            if (rows == null || rows.Count == 0) return null;
            var index = new Dictionary<string, FinancialPeriodData>(rows.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows)
                index[DetailKey(r.Account, r.Subaccount, r.BranchID, r.OrganizationID, r.Ledger)] = r;
            return index;
        }

        private decimal CalculateAccountLineFromDetail(FLRTReportLineItem line, List<FinancialPeriodData> detailRows, List<FinancialPeriodData> openingDetailRows = null, List<FinancialPeriodData> janOpeningDetailRows = null, List<FinancialPeriodData> cumulativeDetailRows = null)
        {
            decimal total = 0m;
            int matched = 0;

            bool isBeginning        = string.Equals(line.BalanceType, FLRTReportLineItem.BalanceTypeValue.Beginning,        StringComparison.OrdinalIgnoreCase);
            bool isJanuaryBeginning = string.Equals(line.BalanceType, FLRTReportLineItem.BalanceTypeValue.JanuaryBeginning, StringComparison.OrdinalIgnoreCase);
            bool isDebit    = string.Equals(line.BalanceType, FLRTReportLineItem.BalanceTypeValue.Debit,    StringComparison.OrdinalIgnoreCase);
            bool isCredit   = string.Equals(line.BalanceType, FLRTReportLineItem.BalanceTypeValue.Credit,   StringComparison.OrdinalIgnoreCase);
            bool isMovement = string.Equals(line.BalanceType, FLRTReportLineItem.BalanceTypeValue.Movement, StringComparison.OrdinalIgnoreCase);

            // For Debit/Credit/Movement use year-to-date cumulative rows (full-year range)
            // so transactions from any month are captured, not just the year-end period.
            bool useCumulative = (isDebit || isCredit || isMovement) && cumulativeDetailRows != null && cumulativeDetailRows.Count > 0;
            var sourceRows = useCumulative ? cumulativeDetailRows : detailRows;

            if (sourceRows == null || sourceRows.Count == 0) return 0m;

            // Pre-build O(1) indexes for opening/jan lookups — replaces O(N) FirstOrDefault per row.
            // Built once here; used inside the loop below.
            var openingIndex    = isBeginning        ? BuildDetailIndex(openingDetailRows)    : null;
            var janOpeningIndex = isJanuaryBeginning ? BuildDetailIndex(janOpeningDetailRows) : null;

            foreach (var row in sourceRows)
            {
                if (!IsAccountInRange(row.Account, line.AccountFrom, line.AccountTo)) continue;

                if (!string.IsNullOrWhiteSpace(line.AccountTypeFilter)
                    && !string.Equals(row.AccountType, line.AccountTypeFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(line.SubaccountFilter)
                    && !string.Equals(row.Subaccount, line.SubaccountFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(line.BranchFilter)
                    && !string.Equals(row.BranchID, line.BranchFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(line.OrganizationFilter)
                    && !string.Equals(row.OrganizationID, line.OrganizationFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(line.LedgerFilter)
                    && !string.Equals(row.Ledger, line.LedgerFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                decimal rawValue;
                if (isBeginning && openingIndex != null)
                {
                    // Opening balance = EndingBalance of the prior fiscal-year-end period for the same dimension combination.
                    string k = DetailKey(row.Account, row.Subaccount, row.BranchID, row.OrganizationID, row.Ledger);
                    rawValue = openingIndex.TryGetValue(k, out FinancialPeriodData openingRow) ? openingRow.EndingBalance : 0m;
                }
                else if (isJanuaryBeginning && janOpeningIndex != null)
                {
                    // Opening balance = BeginningBalance of period 01-{Year} for the same dimension combination.
                    string k = DetailKey(row.Account, row.Subaccount, row.BranchID, row.OrganizationID, row.Ledger);
                    rawValue = janOpeningIndex.TryGetValue(k, out FinancialPeriodData janRow) ? janRow.BeginningBalance : 0m;
                }
                else
                {
                    // For Debit/Credit/Movement, row is already from cumulativeDetailRows (full-year).
                    rawValue = GetBalanceByType(row, line.BalanceType);
                }

                decimal signCorrected = ApplyAccountTypeSign(rawValue, row.AccountType);
                decimal finalValue    = line.SignRule == FLRTReportLineItem.SignRuleValue.Flip
                                        ? signCorrected * -1
                                        : signCorrected;

                total += finalValue;
                matched++;
            }

            if (matched == 0)
            {
                PXTrace.WriteWarning($"    [Engine] No match: range {line.AccountFrom}:{line.AccountTo} filters Sub='{line.SubaccountFilter}' Branch='{line.BranchFilter}' Org='{line.OrganizationFilter}' Ledger='{line.LedgerFilter}'");
                var inRange = sourceRows.Where(r => IsAccountInRange(r.Account, line.AccountFrom, line.AccountTo)).Take(3).ToList();
                foreach (var r in inRange)
                    PXTrace.WriteWarning($"    Sample row: Acct={r.Account} Sub='{r.Subaccount}' Branch='{r.BranchID}' Org='{r.OrganizationID}' Ledger='{r.Ledger}' EndBal={r.EndingBalance}");
            }

            return total;
        }

        // ─────────────────────────────────────────────────────────────────
        // SIGN NORMALIZATION
        // ─────────────────────────────────────────────────────────────────

        private decimal ApplyAccountTypeSign(decimal rawValue, string accountType)
        {
            switch (accountType?.Trim())
            {
                case FLRTReportLineItem.AccountTypeValue.Liability:
                case FLRTReportLineItem.AccountTypeValue.Income:
                case FLRTReportLineItem.AccountTypeValue.Equity:
                    return rawValue * -1;

                case FLRTReportLineItem.AccountTypeValue.Asset:
                case FLRTReportLineItem.AccountTypeValue.Expense:
                default:
                    return rawValue;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // BALANCE TYPE SELECTOR
        // ─────────────────────────────────────────────────────────────────

        private decimal GetBalanceByType(FinancialPeriodData data, string balanceType)
        {
            switch (balanceType?.ToUpper())
            {
                case FLRTReportLineItem.BalanceTypeValue.Beginning:     return data.BeginningBalance;
                case FLRTReportLineItem.BalanceTypeValue.Debit:         return data.Debit;
                case FLRTReportLineItem.BalanceTypeValue.Credit:        return data.Credit;
                case FLRTReportLineItem.BalanceTypeValue.Movement:      return data.Debit - data.Credit;
                // Period types: read the same fields but always from single-period data (no cumulative)
                case FLRTReportLineItem.BalanceTypeValue.PeriodDebit:    return data.Debit;
                case FLRTReportLineItem.BalanceTypeValue.PeriodCredit:   return data.Credit;
                case FLRTReportLineItem.BalanceTypeValue.PeriodMovement: return data.Debit - data.Credit;
                case FLRTReportLineItem.BalanceTypeValue.Ending:
                default:
                    return data.EndingBalance;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // SUBTOTAL CALCULATION
        // Scoped to the same definition (by DefinitionID + Prefix)
        // Looks up child values from the global dictionary
        // ─────────────────────────────────────────────────────────────────

        private decimal CalculateSubtotal(
            string subtotalLineCode,
            int definitionID,
            string prefix,
            Dictionary<string, decimal> globalValues,
            Dictionary<string, List<LineNode>> childrenByParent)
        {
            // Look up children from in-memory map — zero DB queries
            string parentKey = $"{definitionID}_{subtotalLineCode.ToUpper()}";
            if (!childrenByParent.TryGetValue(parentKey, out var childNodes))
                return 0m;

            decimal total = 0m;
            foreach (var child in childNodes)
            {
                if (string.IsNullOrWhiteSpace(child.Line.LineCode)) continue;
                if (globalValues.TryGetValue(child.GlobalKey, out decimal childValue))
                    total += childValue;
            }

            return total;
        }

        // ─────────────────────────────────────────────────────────────────
        // FORMULA EVALUATOR
        // Resolves tokens as PREFIX_LINECODE, supports both explicit and
        // implicit prefix syntax.
        // ─────────────────────────────────────────────────────────────────

        private decimal EvaluateFormula(
            string formula,
            string currentPrefix,
            HashSet<string> knownPrefixes,
            Dictionary<string, decimal> globalValues)
        {
            if (string.IsNullOrWhiteSpace(formula)) return 0m;

            try
            {
                var tokens = TokenizeFormula(formula);
                return EvaluateTokens(tokens, currentPrefix, knownPrefixes, globalValues);
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"ReportCalculationEngine: Formula evaluation failed for '{formula}': {ex.Message}");
                return 0m;
            }
        }

        private List<string> TokenizeFormula(string formula)
        {
            var tokens = new List<string>();
            foreach (Match m in FormulaTokenRegex.Matches(formula.Trim()))
                tokens.Add(m.Value);
            return tokens;
        }

        private decimal EvaluateTokens(
            List<string> tokens,
            string currentPrefix,
            HashSet<string> knownPrefixes,
            Dictionary<string, decimal> globalValues)
        {
            int pos = 0;
            return ParseExpression(tokens, ref pos, currentPrefix, knownPrefixes, globalValues);
        }

        private decimal ParseExpression(
            List<string> tokens, ref int pos,
            string currentPrefix, HashSet<string> knownPrefixes,
            Dictionary<string, decimal> globalValues)
        {
            decimal result = ParseTerm(tokens, ref pos, currentPrefix, knownPrefixes, globalValues);

            while (pos < tokens.Count && (tokens[pos] == "+" || tokens[pos] == "-"))
            {
                string op = tokens[pos++];
                decimal right = ParseTerm(tokens, ref pos, currentPrefix, knownPrefixes, globalValues);
                result = op == "+" ? result + right : result - right;
            }

            return result;
        }

        private decimal ParseTerm(
            List<string> tokens, ref int pos,
            string currentPrefix, HashSet<string> knownPrefixes,
            Dictionary<string, decimal> globalValues)
        {
            decimal result = ParseFactor(tokens, ref pos, currentPrefix, knownPrefixes, globalValues);

            while (pos < tokens.Count && (tokens[pos] == "*" || tokens[pos] == "/"))
            {
                string op = tokens[pos++];
                decimal right = ParseFactor(tokens, ref pos, currentPrefix, knownPrefixes, globalValues);
                if (op == "/")
                {
                    if (right != 0)
                        result = result / right;
                    else
                    {
                        PXTrace.WriteWarning("[Engine] Division by zero in formula — result set to 0.");
                        result = 0m;
                    }
                }
                else if (op == "*")
                    result = result * right;
            }

            return result;
        }

        private decimal ParseFactor(
            List<string> tokens, ref int pos,
            string currentPrefix, HashSet<string> knownPrefixes,
            Dictionary<string, decimal> globalValues)
        {
            if (pos >= tokens.Count) return 0m;

            string token = tokens[pos];

            if (token == "(")
            {
                pos++;
                decimal inner = ParseExpression(tokens, ref pos, currentPrefix, knownPrefixes, globalValues);
                if (pos < tokens.Count && tokens[pos] == ")")
                    pos++;
                return inner;
            }

            if (token == "-")
            {
                pos++;
                return -ParseFactor(tokens, ref pos, currentPrefix, knownPrefixes, globalValues);
            }

            pos++;

            if (decimal.TryParse(token, out decimal numericValue))
                return numericValue;

            // Resolve token to global key and look up
            string globalKey = ResolveToken(token, currentPrefix, knownPrefixes);
            if (globalValues.TryGetValue(globalKey, out decimal lineValue))
                return lineValue;

            PXTrace.WriteWarning($"ReportCalculationEngine: Formula references unknown key '{globalKey}' (token: '{token}'). Defaulting to 0.");
            return 0m;
        }

        // ─────────────────────────────────────────────────────────────────
        // ACCOUNT RANGE COMPARISON
        // ─────────────────────────────────────────────────────────────────

        private bool IsAccountInRange(string account, string from, string to)
        {
            return CompareAccountCodes(account, from) >= 0
                && CompareAccountCodes(account, to)   <= 0;
        }

        private int CompareAccountCodes(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 0;
            if (string.IsNullOrEmpty(a)) return -1;
            if (string.IsNullOrEmpty(b)) return 1;

            bool segA = a.Contains('-');
            bool segB = b.Contains('-');

            if (segA && segB)
            {
                var segsA = a.Split('-');
                var segsB = b.Split('-');
                int min = Math.Min(segsA.Length, segsB.Length);
                for (int s = 0; s < min; s++)
                {
                    bool numA = int.TryParse(segsA[s], out int nA);
                    bool numB = int.TryParse(segsB[s], out int nB);
                    int cmp = (numA && numB)
                        ? nA.CompareTo(nB)
                        : string.Compare(segsA[s], segsB[s], StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0) return cmp;
                }
                return segsA.Length.CompareTo(segsB.Length);
            }

            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    long n1 = 0; while (i < a.Length && char.IsDigit(a[i])) n1 = n1 * 10 + (a[i++] - '0');
                    long n2 = 0; while (j < b.Length && char.IsDigit(b[j])) n2 = n2 * 10 + (b[j++] - '0');
                    if (n1 != n2) return n1.CompareTo(n2);
                }
                else
                {
                    int cmp = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                    if (cmp != 0) return cmp;
                    i++; j++;
                }
            }

            return a.Length.CompareTo(b.Length);
        }

        // ─────────────────────────────────────────────────────────────────
        // BUILD PLACEHOLDER MAP
        // Keys: PREFIX_LINECODE_CY / PREFIX_LINECODE_PY
        // Values: formatted strings (per-definition rounding applied)
        // ─────────────────────────────────────────────────────────────────

        private Dictionary<string, string> BuildPlaceholderMap(List<LineNode> allNodes)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var node in allNodes)
            {
                if (string.IsNullOrWhiteSpace(node.Line.LineCode)) continue;

                string cyKey = $"{node.Prefix}_{node.Line.LineCode}_{Constants.CurrentYearSuffix}";
                string pyKey = $"{node.Prefix}_{node.Line.LineCode}_{Constants.PreviousYearSuffix}";
                string pmKey = $"{node.Prefix}_{node.Line.LineCode}_{Constants.PreviousMonthSuffix}";

                if (node.Line.LineType == FLRTReportLineItem.LineItemType.Heading || node.Line.IsVisible == false)
                {
                    map[cyKey] = string.Empty;
                    map[pyKey] = string.Empty;
                    map[pmKey] = string.Empty;
                    continue;
                }

                decimal cyVal = _cyGlobal.TryGetValue(node.GlobalKey, out decimal cy) ? cy : 0m;
                decimal pyVal = _pyGlobal.TryGetValue(node.GlobalKey, out decimal py) ? py : 0m;
                decimal pmVal = _pmGlobal.TryGetValue(node.GlobalKey, out decimal pm) ? pm : 0m;

                map[cyKey] = FormatFinancialValue(cyVal, node.Rounding);
                map[pyKey] = FormatFinancialValue(pyVal, node.Rounding);
                map[pmKey] = FormatFinancialValue(pmVal, node.Rounding);
            }

            return map;
        }

        // ─────────────────────────────────────────────────────────────────
        // VALUE FORMATTING
        // ─────────────────────────────────────────────────────────────────

        private string FormatFinancialValue(decimal value, RoundingSettings rounding = null)
        {
            rounding = rounding ?? _rounding;
            decimal scaled = ApplyRounding(value, rounding);

            if (scaled == 0m) return "-";

            string format = BuildFormatString(rounding);
            if (scaled < 0m) return $"({Math.Abs(scaled).ToString(format)})";
            return scaled.ToString(format);
        }

        private decimal ApplyRounding(decimal value, RoundingSettings rounding = null)
        {
            rounding = rounding ?? _rounding;

            switch (rounding.RoundingLevel)
            {
                case FLRTReportDefinition.RoundingLevelType.Thousands:
                    value = value / 1000m;
                    break;
                case FLRTReportDefinition.RoundingLevelType.Millions:
                    value = value / 1000000m;
                    break;
            }

            return Math.Round(value, rounding.DecimalPlaces, MidpointRounding.AwayFromZero);
        }

        private string BuildFormatString(RoundingSettings rounding = null)
        {
            rounding = rounding ?? _rounding;
            if (rounding.DecimalPlaces <= 0)
                return "#,##0";
            return "#,##0." + new string('0', rounding.DecimalPlaces);
        }
    }
}
