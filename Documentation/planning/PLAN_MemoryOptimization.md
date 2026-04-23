# Plan: Memory Optimization — Prevent Application Overload During Report Generation

## Context

Report generation fetches financial data from up to 8 parallel OData API calls, each returning potentially tens of thousands of JToken rows. The current implementation holds all results in memory simultaneously, creating peak memory spikes that can cause `OutOfMemoryException` or severe GC pauses on IIS worker processes with limited memory budgets (typically 1–2 GB).

This plan implements 5 targeted optimizations across 2 files to reduce peak memory consumption by an estimated 60–80%, preventing the application from overloading during report generation.

---

## Item 1: Limit API Fetch Concurrency with SemaphoreSlim(3)

**File:** `Services\ReportGenerationService.cs`

**Problem:** Lines 142–158 fire all 8 `Task.Run` calls simultaneously. Each fetch can return a large `List<JToken>` + build a `FinancialApiData` object. With 8 concurrent fetches, peak memory holds all 8 result sets at once.

**Solution:** Wrap each fetch lambda with a shared `SemaphoreSlim(3)` so at most 3 fetches execute concurrently. The remaining tasks queue and execute as slots free up.

### Changes:

**Add semaphore declaration** before the fetch block (after line 141, before the `Task.Run` calls):

```csharp
var fetchGate = new SemaphoreSlim(3, 3);
```

**Wrap each `Task.Run` lambda** with semaphore acquire/release. Replace lines 142–158 with:

```csharp
var taskCY      = Task.Run(async () => { await fetchGate.WaitAsync(cancellationToken); try { return localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, selectedPeriod, needsDetail, cancellationToken); } finally { fetchGate.Release(); } }, cancellationToken);
var taskPY      = Task.Run(async () => { await fetchGate.WaitAsync(cancellationToken); try { return localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYearPeriod, needsDetail, cancellationToken); } finally { fetchGate.Release(); } }, cancellationToken);
var taskJanPY   = Task.Run(async () => { await fetchGate.WaitAsync(cancellationToken); try { return localDataService.FetchJanuaryBeginningBalance(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYear, cancellationToken); } finally { fetchGate.Release(); } }, cancellationToken);
var taskJanCY   = Task.Run(async () => { await fetchGate.WaitAsync(cancellationToken); try { return localDataService.FetchJanuaryBeginningBalance(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, currYear, cancellationToken); } finally { fetchGate.Release(); } }, cancellationToken);
var taskRangeCY = needsCumulative
    ? Task.Run(async () => { await fetchGate.WaitAsync(cancellationToken); try { return localDataService.FetchRangeApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, cyCumulativeStart, selectedPeriod, cancellationToken); } finally { fetchGate.Release(); } }, cancellationToken)
    : Task.FromResult<FinancialApiData>(null);
var taskRangePY = needsCumulative
    ? Task.Run(async () => { await fetchGate.WaitAsync(cancellationToken); try { return localDataService.FetchRangeApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, pyCumulativeStart, pyCumulativeEnd, cancellationToken); } finally { fetchGate.Release(); } }, cancellationToken)
    : Task.FromResult<FinancialApiData>(null);
var taskPrior   = Task.Run(async () => { await fetchGate.WaitAsync(cancellationToken); try { return localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYearPriorPeriod, needsDetail, cancellationToken); } finally { fetchGate.Release(); } }, cancellationToken);
var taskPM      = Task.Run(async () => { await fetchGate.WaitAsync(cancellationToken); try { return localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevMonthPeriod, needsDetail, cancellationToken); } finally { fetchGate.Release(); } }, cancellationToken);
```

**Add `fetchGate.Dispose()`** in the `finally` block (after the credential cache clear):

```csharp
fetchGate?.Dispose();
```

> Note: Move `fetchGate` declaration to method scope (before `try`) so it's accessible in `finally`.

### Keep unchanged:
- `Task.WhenAll(...).Wait()` — still waits for all 8 tasks
- Result destructuring — unchanged
- `Task.FromResult<FinancialApiData>(null)` for skipped cumulative fetches — these don't acquire the semaphore

---

## Item 2: Stream-Aggregate in PaginatedFetchAsync (Callback Pattern)

**File:** `Services\FinancialDataService.cs`

**Problem:** `PaginatedFetchAsync` (line 377) accumulates all rows into `List<JToken> allResults`, then returns the entire list. The caller (`FetchAllApiDataAsync`, `FetchJanuaryBeginningBalance`, `FetchRangeApiData`) iterates the list a second time to build `FinancialApiData`. This means the full JToken list and the FinancialApiData coexist in memory.

**Solution:** Add a new overload of `PaginatedFetchAsync` that accepts an `Action<JToken>` callback. Each row is passed to the callback as it's parsed, then the JToken reference is dropped. The caller aggregates directly — no intermediate list.

### 2a. Add streaming overload to PaginatedFetchAsync

Add this new method after the existing `PaginatedFetchAsync` (after line 440):

```csharp
/// <summary>
/// Streaming variant of PaginatedFetchAsync. Instead of accumulating all rows into a list,
/// each row is passed to the <paramref name="rowConsumer"/> callback and then discarded.
/// This keeps peak memory at one page of JTokens rather than the entire result set.
/// Returns the total number of rows processed, or -1 if all fetch attempts failed.
/// </summary>
private async Task<int> PaginatedFetchStreamAsync(
    HttpClient client, string baseUrl, string filter, string selectColumns,
    string accessToken, Action<JToken> rowConsumer,
    CancellationToken cancellationToken = default, int maxRows = 100_000)
{
    int totalRows = 0;
    int pageSize = 10_000;
    int skip = 0;

    while (true)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string encodedFilter = Uri.EscapeDataString(filter);
        string encodedSelect = Uri.EscapeDataString(selectColumns);
        string pagedUrl = $"{baseUrl}?$filter={encodedFilter}&$select={encodedSelect}&$top={pageSize}&$skip={skip}";

        HttpResponseMessage response;
        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, pagedUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                response = await client.SendAsync(request, cancellationToken);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PXTrace.WriteError($"HTTP request to {pagedUrl} failed: {ex.Message}");
            return -1;
        }

        if (!response.IsSuccessStatusCode)
        {
            PXTrace.WriteWarning($"Request to {pagedUrl} returned status {response.StatusCode}.");
            return -1;
        }

        string jsonResponse = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(jsonResponse) || !jsonResponse.TrimStart().StartsWith("{"))
        {
            PXTrace.WriteError("API returned a non-JSON or empty response.");
            return -1;
        }

        JObject parsed = JObject.Parse(jsonResponse);
        var pageResults = parsed["value"] as JArray;

        if (pageResults == null || pageResults.Count == 0)
            break;

        foreach (var row in pageResults)
            rowConsumer(row);

        totalRows += pageResults.Count;
        skip += pageSize;

        // Explicitly clear page data to allow GC before next page
        pageResults.Clear();
        parsed.RemoveAll();

        if (totalRows >= maxRows)
        {
            PXTrace.WriteWarning($"[FinancialDataService] Row cap reached: {totalRows} rows (limit={maxRows}) from {baseUrl}.");
            break;
        }
    }

    PXTrace.WriteInformation($"Successfully streamed {totalRows} total records for base URL: {baseUrl}");
    return totalRows;
}
```

### 2b. Add streaming fallback wrapper

Add this method after the new `PaginatedFetchStreamAsync`:

```csharp
/// <summary>
/// Streaming variant of ExecuteFetchWithFallbackAsync. Tries all 4 URL/Ledger combinations
/// and streams rows to the callback. Returns total row count, or throws if all attempts fail.
/// IMPORTANT: The caller must reset its aggregation state before each retry. This method
/// calls <paramref name="resetConsumer"/> before each fallback attempt.
/// </summary>
private async Task<int> ExecuteFetchStreamWithFallbackAsync(
    HttpClient client, string baseFilter, string ledger, string accessToken,
    Action<JToken> rowConsumer, Action resetConsumer,
    CancellationToken cancellationToken = default)
{
    string giName = _columnMapping.GIName;
    string modernUrlBase = $"{_baseUrl}/odata/{_tenantName}/{giName}";
    string legacyUrlBase = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/{giName}";
    string selectColumns = _columnMapping.BuildSelectColumns();

    string filterWithLedger = AppendLedgerFilter(baseFilter, ledger);

    // Attempt 1: Modern URL with Ledger
    resetConsumer();
    int count = await PaginatedFetchStreamAsync(client, modernUrlBase, filterWithLedger, selectColumns, accessToken, rowConsumer, cancellationToken);
    if (count >= 0) return count;

    // Attempt 2: Modern URL without Ledger
    PXTrace.WriteWarning($"Fallback 2: Modern URL without Ledger. URL: {modernUrlBase}, Filter: {baseFilter}");
    resetConsumer();
    count = await PaginatedFetchStreamAsync(client, modernUrlBase, baseFilter, selectColumns, accessToken, rowConsumer, cancellationToken);
    if (count >= 0) return count;

    // Attempt 3: Legacy URL with Ledger
    PXTrace.WriteWarning($"Attempt 2 failed. Retrying with Legacy URL with Ledger. URL: {legacyUrlBase}, Filter: {filterWithLedger}");
    resetConsumer();
    count = await PaginatedFetchStreamAsync(client, legacyUrlBase, filterWithLedger, selectColumns, accessToken, rowConsumer, cancellationToken);
    if (count >= 0) return count;

    // Attempt 4: Legacy URL without Ledger
    PXTrace.WriteWarning($"Attempt 3 failed. Retrying with Legacy URL without Ledger. URL: {legacyUrlBase}, Filter: {baseFilter}");
    resetConsumer();
    count = await PaginatedFetchStreamAsync(client, legacyUrlBase, baseFilter, selectColumns, accessToken, rowConsumer, cancellationToken);
    if (count >= 0) return count;

    PXTrace.WriteError("All streaming fetch attempts failed.");
    return -1;
}
```

### 2c. Convert FetchAllApiDataAsync to use streaming

Replace the body of `FetchAllApiDataAsync` (lines 63–149) with:

```csharp
public async Task<FinancialApiData> FetchAllApiDataAsync(string branch, string organization, string ledger, string period, bool includeDetail = true, CancellationToken cancellationToken = default)
{
    string accessToken = await _authService.AuthenticateAndGetTokenAsync();
    string dimensionFilter = BuildDimensionFilter(branch, organization);
    string filter = $"FinancialPeriod eq '{period}' and {dimensionFilter}";

    var accountData = new Dictionary<string, FinancialPeriodData>();
    var detailRows  = new List<FinancialPeriodData>();
    bool firstRowLogged = false;

    Action<JToken> rowConsumer = (item) =>
    {
        // Diagnostic: log OData columns from the first row
        if (!firstRowLogged && item is JObject firstObj)
        {
            var propNames = firstObj.Properties().Select(p => p.Name).ToList();
            PXTrace.WriteInformation($"OData columns ({propNames.Count}): {string.Join(", ", propNames)}");
            firstRowLogged = true;
        }

        string accountId = item[_columnMapping.AccountColumn]?.ToString();
        if (string.IsNullOrEmpty(accountId)) return;

        string subaccount  = item[_columnMapping.SubaccountColumn]?.ToString()?.Trim()   ?? string.Empty;
        string branchId    = item[_columnMapping.BranchColumn]?.ToString()?.Trim()      ?? string.Empty;
        string orgId       = item[_columnMapping.OrganizationColumn]?.ToString()?.Trim() ?? string.Empty;
        string ledgerId    = item[_columnMapping.LedgerColumn]?.ToString()?.Trim()      ?? string.Empty;
        string accountType = item[_columnMapping.TypeColumn]?.ToString() ?? string.Empty;
        decimal begBal  = item[_columnMapping.BeginningBalCol]?.ToObject<decimal>() ?? 0;
        decimal endBal  = item[_columnMapping.EndingBalCol]?.ToObject<decimal>()    ?? 0;
        decimal debit   = item[_columnMapping.DebitColumn]?.ToObject<decimal>()     ?? 0;
        decimal credit  = item[_columnMapping.CreditColumn]?.ToObject<decimal>()    ?? 0;

        if (!accountData.TryGetValue(accountId!, out var acctEntry))
        {
            acctEntry = new FinancialPeriodData { Account = accountId, AccountType = accountType };
            accountData[accountId!] = acctEntry;
        }
        acctEntry.BeginningBalance += begBal;
        acctEntry.EndingBalance    += endBal;
        acctEntry.Debit            += debit;
        acctEntry.Credit           += credit;

        if (includeDetail)
        {
            detailRows.Add(new FinancialPeriodData
            {
                Account = accountId, Subaccount = subaccount, AccountType = accountType,
                BranchID = branchId, OrganizationID = orgId, Ledger = ledgerId,
                BeginningBalance = begBal, EndingBalance = endBal, Debit = debit, Credit = credit
            });
        }
    };

    Action resetConsumer = () =>
    {
        accountData.Clear();
        detailRows.Clear();
        firstRowLogged = false;
    };

    int totalRows = await ExecuteFetchStreamWithFallbackAsync(_httpClient, filter, ledger, accessToken, rowConsumer, resetConsumer, cancellationToken);

    if (totalRows < 0)
        throw new PXException(Messages.FailedToFetchOData);

    PXTrace.WriteInformation($"FetchAllApiData: {accountData.Count} aggregated accounts, {detailRows.Count} detail rows.");
    if (detailRows.Count > 0)
    {
        var first = detailRows[0];
        PXTrace.WriteInformation($"  First detail row: Account={first.Account}, Sub={first.Subaccount}, Branch={first.BranchID}, Org={first.OrganizationID}, Ledger={first.Ledger}, EndBal={first.EndingBalance}");
        var sampleAccount = detailRows[0].Account;
        var subs = detailRows.Where(r => r.Account == sampleAccount).Select(r => r.Subaccount).Distinct().ToList();
        PXTrace.WriteInformation($"  Subaccounts for {sampleAccount}: [{string.Join(", ", subs)}]");
    }

    return new FinancialApiData { AccountData = accountData, DetailRows = detailRows };
}
```

### 2d. Convert FetchJanuaryBeginningBalance to use streaming

Replace the body of `FetchJanuaryBeginningBalance` (lines 153–187) with:

```csharp
public FinancialApiData FetchJanuaryBeginningBalance(string branch, string organization, string ledger, string prevYear, CancellationToken cancellationToken = default)
{
    string januaryPeriod = "01" + prevYear;
    string accessToken = _authService.AuthenticateAndGetToken();
    string dimensionFilter = BuildDimensionFilter(branch, organization);
    string baseFilter = $"FinancialPeriod eq '{januaryPeriod}' and {dimensionFilter}";

    var apiData = new FinancialApiData();

    Action<JToken> rowConsumer = (item) =>
    {
        string accountId = item[_columnMapping.AccountColumn]?.ToString();
        decimal beginningBalance = item[_columnMapping.BeginningBalCol]?.ToObject<decimal>() ?? 0;
        if (string.IsNullOrEmpty(accountId)) return;

        if (!apiData.AccountData.TryGetValue(accountId!, out var janEntry))
        {
            janEntry = new FinancialPeriodData();
            apiData.AccountData[accountId!] = janEntry;
        }
        janEntry.BeginningBalance += beginningBalance;
        janEntry.EndingBalance += beginningBalance;
    };

    Action resetConsumer = () => { apiData = new FinancialApiData(); };

    int count = ExecuteFetchStreamWithFallbackAsync(_httpClient, baseFilter, ledger, accessToken, rowConsumer, resetConsumer, cancellationToken).Result;

    if (count < 0)
        throw new PXException(Messages.FailedToFetchOData);

    return apiData;
}
```

### 2e. Convert FetchRangeApiData to use streaming

Replace the body of `FetchRangeApiData` (lines 192–233) with:

```csharp
public FinancialApiData FetchRangeApiData(string branch, string organization, string ledger, string fromPeriod, string toPeriod, CancellationToken cancellationToken = default)
{
    string accessToken = _authService.AuthenticateAndGetToken();
    string dimensionFilter = BuildDimensionFilter(branch, organization);
    string baseFilter = $"FinancialPeriod ge '{fromPeriod}' and FinancialPeriod le '{toPeriod}' and {dimensionFilter}";

    var cumulativeDict = new Dictionary<string, FinancialPeriodData>();

    Action<JToken> rowConsumer = (item) =>
    {
        string accountId = item[_columnMapping.AccountColumn]?.ToString();
        decimal debit = item[_columnMapping.DebitColumn]?.ToObject<decimal>() ?? 0;
        decimal credit = item[_columnMapping.CreditColumn]?.ToObject<decimal>() ?? 0;
        decimal endingBalance = item[_columnMapping.EndingBalCol]?.ToObject<decimal>() ?? 0;
        if (string.IsNullOrEmpty(accountId)) return;

        if (!cumulativeDict.TryGetValue(accountId!, out var cumEntry))
        {
            cumEntry = new FinancialPeriodData();
            cumulativeDict[accountId!] = cumEntry;
        }
        cumEntry.Debit += debit;
        cumEntry.Credit += credit;
        cumEntry.EndingBalance += endingBalance;
    };

    Action resetConsumer = () => { cumulativeDict.Clear(); };

    int count = ExecuteFetchStreamWithFallbackAsync(_httpClient, baseFilter, ledger, accessToken, rowConsumer, resetConsumer, cancellationToken).Result;

    if (count < 0)
        throw new PXException(Messages.FailedToFetchOData);

    var apiData = new FinancialApiData();
    foreach (var kvp in cumulativeDict)
        apiData.AccountData[kvp.Key] = kvp.Value;

    return apiData;
}
```

### Keep unchanged:
- `FetchCompositeKeyData` — low-volume, not on the critical path
- `FetchEndingBalance` — single-row fetch, no memory concern
- `FetchGIColumns` — single-row fetch
- The original `PaginatedFetchAsync` and `ExecuteFetchWithFallbackAsync` — kept for backward compatibility with `FetchCompositeKeyData` and `FetchEndingBalance`

---

## Item 3: Null `templateFileContent` After Writing to Disk

**File:** `Services\ReportGenerationService.cs`

**Problem:** `templateFileContent` (a `byte[]` that can be several MB) stays alive on the stack from line 68 until the method exits, even though it's only needed until `File.WriteAllBytes` on line 80.

**Solution:** Add `templateFileContent = null;` immediately after line 80.

### Change:

After this line (line 80):
```csharp
File.WriteAllBytes(templatePath, templateFileContent);
```

Add:
```csharp
templateFileContent = null; // Free template bytes before the long fetch phase
```

---

## Item 4: Null `generatedFileContent` After SaveGeneratedDocument

**File:** `Services\ReportGenerationService.cs`

**Problem:** `generatedFileContent` (the generated Word doc as `byte[]`) stays alive from line 220 until method exit, even though `SaveGeneratedDocument` on line 221 is the last consumer.

**Solution:** Add `generatedFileContent = null;` immediately after the `SaveGeneratedDocument` call.

### Change:

After this line (line 221):
```csharp
var fileId = _fileService.SaveGeneratedDocument(outputFileName, generatedFileContent, _currentRecord);
```

Add:
```csharp
generatedFileContent = null; // Free generated file bytes immediately
```

---

## Item 5: Use JsonTextReader Instead of JObject.Parse in PaginatedFetchStreamAsync

**File:** `Services\FinancialDataService.cs`

**Problem:** In `PaginatedFetchStreamAsync` (from Item 2), the line `JObject parsed = JObject.Parse(jsonResponse)` loads the entire JSON string into a JObject tree — doubling memory for that page (string + object tree).

**Solution:** Replace `ReadAsStringAsync` + `JObject.Parse` with `ReadAsStreamAsync` + `JsonTextReader` to parse directly from the HTTP response stream. Each row is deserialized individually and passed to the callback.

**Prerequisite:** Item 2 must be implemented first (this modifies `PaginatedFetchStreamAsync`).

### Add using directive:

Add to the top of `FinancialDataService.cs`:
```csharp
using Newtonsoft.Json;
```

### Replace the page-reading section in PaginatedFetchStreamAsync:

Replace this block inside the `while (true)` loop:
```csharp
string jsonResponse = await response.Content.ReadAsStringAsync();

if (string.IsNullOrWhiteSpace(jsonResponse) || !jsonResponse.TrimStart().StartsWith("{"))
{
    PXTrace.WriteError("API returned a non-JSON or empty response.");
    return -1;
}

JObject parsed = JObject.Parse(jsonResponse);
var pageResults = parsed["value"] as JArray;

if (pageResults == null || pageResults.Count == 0)
    break;

foreach (var row in pageResults)
    rowConsumer(row);

totalRows += pageResults.Count;
skip += pageSize;

pageResults.Clear();
parsed.RemoveAll();
```

With:
```csharp
int pageRowCount = 0;
using (var stream = await response.Content.ReadAsStreamAsync())
using (var sr = new System.IO.StreamReader(stream))
using (var reader = new JsonTextReader(sr))
{
    // Navigate to the "value" array
    bool foundValue = false;
    while (reader.Read())
    {
        if (reader.TokenType == JsonToken.PropertyName && (string)reader.Value == "value")
        {
            reader.Read(); // Move to StartArray
            if (reader.TokenType == JsonToken.StartArray)
            {
                foundValue = true;
                break;
            }
        }
    }

    if (!foundValue)
        break; // No "value" array — treat as empty page

    // Read each object in the array
    while (reader.Read() && reader.TokenType != JsonToken.EndArray)
    {
        if (reader.TokenType == JsonToken.StartObject)
        {
            var row = JObject.Load(reader);
            rowConsumer(row);
            pageRowCount++;
        }
    }
}

if (pageRowCount == 0)
    break;

totalRows += pageRowCount;
skip += pageSize;
```

---

## Files Modified (summary)

| File | Items | Action |
|------|-------|--------|
| `Services\ReportGenerationService.cs` | 1, 3, 4 | Add SemaphoreSlim(3) around fetch tasks, null byte arrays early |
| `Services\FinancialDataService.cs` | 2, 5 | Add streaming fetch methods, convert 3 Fetch methods to callbacks, use JsonTextReader |

---

## Implementation Order

1. **Items 3 & 4** — One-line changes, zero risk. Do first.
2. **Item 1** — SemaphoreSlim. Moderate effort, high impact. Independent of Items 2/5.
3. **Item 2** — Stream-aggregate callback. Most invasive. Test thoroughly.
4. **Item 5** — JsonTextReader. Builds on Item 2. Apply last.

---

## Verification

1. **Build** the DLL — confirm no compilation errors
2. **Generate a report** with a known-good template and verify placeholder values match expected output
3. **Generate a report with a large chart of accounts** (1000+ accounts) — verify no OOM or timeout
4. **Check trace logs** — verify fetch counts, row counts, and timing are logged correctly
5. **Compare wall-clock time** — expect ≤15% increase from SemaphoreSlim(3), offset by reduced GC pressure
6. **Monitor IIS worker memory** (Task Manager or PerfMon) during generation — peak should be noticeably lower than before
