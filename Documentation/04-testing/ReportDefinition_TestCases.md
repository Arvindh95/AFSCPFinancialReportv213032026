# Report Definition Screen — Comprehensive Test Cases
**Screen:** FR101002 — Report Definition Maintenance
**Date:** 2026-04-28
**Version:** AFSCPFinancialReport v2.1.3
**Balance type model:** 5 FY-to-date types (`ENDING`, `BEGINNING`, `DEBIT`, `CREDIT`, `MOVEMENT`). No single-period balance types.
**Placeholders emitted:** `_CY` and `_PY` only. No `_PM`.

---

## How to Use This Document

Each test case follows this structure:

| Field | Meaning |
|-------|---------|
| **ID** | Unique test ID — reference this when logging results |
| **Objective** | What behaviour is being verified |
| **Prerequisites** | What must exist before running the test |
| **Steps** | Exact steps to perform in the UI |
| **Expected Result** | What must happen for the test to pass |
| **Pass/Fail** | Tester fills in after running |
| **Notes** | Tester fills in — actual result, deviations, screenshots |

### General Prerequisites (apply to all tests)
- Acumatica ERP 2025R2 running with AFSCPFinancialReport CP published
- Logged in as `admin`
- TrialBalance GI configured and returning data
- At least 2 branches, 1 organization, 1 ledger (`ACTUAL`) exist with GL history
- Tenant credentials configured on FR Tenant Credentials screen

---

## SECTION A — Definition Header

---

### A-01: Create a New Definition
**Objective:** Verify a definition can be created and saved successfully.

**Prerequisites:** No existing definition with CD `TEST-BS`.

**Steps:**
1. Navigate to **Finance → Financial Reports → Report Definitions** (FR101002)
2. Click **+** (Insert)
3. Fill in:
   - Definition Code: `TEST-BS`
   - Prefix: `BS`
   - Description: `Balance Sheet Test`
   - Report Type: `Balance Sheet`
   - Generic Inquiry Name: `AFS-Trial-Balance`
4. Click **Save**
5. Navigate away and reopen `TEST-BS`

**Expected Result:**
- Record saved successfully
- Definition Code field is **disabled** (locked)
- Prefix field is **disabled** (locked)
- All other fields remain editable

**Pass/Fail:** ___
**Notes:** ___

---

### A-02: Duplicate Definition Code
**Objective:** Verify duplicate DefinitionCD is rejected.

**Prerequisites:** Definition `TEST-BS` exists from A-01.

**Steps:**
1. Click **+** (Insert)
2. Enter Definition Code: `TEST-BS`
3. Enter Prefix: `BS2`
4. Click **Save**

**Expected Result:**
- Error displayed: *"Definition Code must be unique."*
- Record not saved

**Pass/Fail:** ___
**Notes:** ___

---

### A-03: Duplicate Prefix
**Objective:** Verify duplicate Prefix is rejected across definitions.

**Prerequisites:** Definition with Prefix `BS` exists.

**Steps:**
1. Click **+** (Insert)
2. Enter Definition Code: `TEST-BS2`
3. Enter Prefix: `BS`
4. Click **Save**

**Expected Result:**
- Error displayed: *"Definition Prefix must be unique across all definitions. Another definition already uses this prefix."*
- Record not saved

**Pass/Fail:** ___
**Notes:** ___

---

### A-04: Prefix with Special Characters
**Objective:** Verify prefix rejects non-alphanumeric characters.

**Steps:**
1. Create new definition
2. Enter Prefix: `BS_01` (underscore)
3. Click **Save**
4. Repeat with Prefix: `BS 01` (space)
5. Repeat with Prefix: `BS-01` (hyphen)

**Expected Result:**
- All three attempts fail with error: *"Definition Prefix must contain letters and digits only — no spaces, underscores, or special characters."*

**Pass/Fail:** ___
**Notes:** ___

---

### A-05: Missing Required Fields — Definition Header
**Objective:** Verify all required fields are enforced.

**Steps:**
1. Click **+** (Insert)
2. Leave **Definition Code** blank, fill all others → Save
3. Then leave **Prefix** blank, fill all others → Save

**Expected Result:**
- Step 2: Error on Definition Code field
- Step 3: Error on Prefix field

**Pass/Fail:** ___
**Notes:** ___

---

### A-06: Rounding — Units
**Objective:** Verify Units rounding outputs full value.

**Prerequisites:** A definition with a line returning value ~1,234,567.

**Steps:**
1. Set Rounding Level = `Units`, Decimal Places = `0`
2. Generate report or use Test Fetch
3. Observe placeholder value

**Expected Result:**
- Value displayed as `1,235` (rounded to nearest unit, no division)
- Wait — Units with 0 decimal = `1,234,567`

**Expected Result (corrected):**
- `1,234,567` — no scaling, rounded to 0 decimal places

**Pass/Fail:** ___
**Notes:** ___

---

### A-07: Rounding — Thousands
**Objective:** Verify Thousands rounding divides by 1,000.

**Steps:**
1. Set Rounding Level = `Thousands`, Decimal Places = `0`
2. Value in GL = 1,234,567

**Expected Result:**
- Output = `1,235` (1,234,567 / 1,000 = 1,234.567 → rounded to 0dp = 1,235)

**Pass/Fail:** ___
**Notes:** ___

---

### A-08: Rounding — Millions
**Objective:** Verify Millions rounding divides by 1,000,000.

**Steps:**
1. Set Rounding Level = `Millions`, Decimal Places = `2`
2. Value in GL = 1,234,567

**Expected Result:**
- Output = `1.23` (1,234,567 / 1,000,000 = 1.234567 → rounded to 2dp = 1.23)

**Pass/Fail:** ___
**Notes:** ___

---

### A-09: Column Mapping Selectors Bound to Selected GI
**Objective:** Verify each of the 12 GI-column selectors lists only the columns of the GI chosen in **Generic Inquiry Name**.

**Prerequisites:** Two published GIs exist with different column sets (e.g. `AFS-Trial-Balance` and a clone with renamed columns).

**Steps:**
1. Pick `AFS-Trial-Balance` as Generic Inquiry Name
2. Open the **Account Column** selector — confirm it lists `Account`, `Type`, `BeginningBalance`, `EndingBalance`, `Debit`, `Credit`, `Movement`, `FinancialPeriod`, `Subaccount`, `BranchID`, `OrganizationID`, `LedgerID` etc.
3. Switch Generic Inquiry Name to the cloned GI
4. Re-open the same selector — confirm only the cloned GI's columns appear

**Expected Result:**
- Selector contents follow the chosen GI
- Stale picks from the previous GI are blanked or remain editable so the user can rebind

**Pass/Fail:** ___
**Notes:** ___

---

> **Removed.** A-10 (manual *Detect Columns* button) was dropped — column resolution is now driven entirely by the per-field GI-column selectors above. There is no `Detect Columns` action on FR101002 anymore.

---

## SECTION B — ACCOUNT Line Type

---

### B-01: Basic Account Range — Ending Balance
**Objective:** Verify basic account range sums EndingBalance correctly.

**Prerequisites:** GL accounts `10100–10199` (Cash) have ending balance data.

**Steps:**
1. Add line to definition:
   - Line Code: `CASH`
   - Description: `Cash and Cash Equivalents`
   - Line Type: `Account Range`
   - Account From: `10100`
   - Account To: `10199`
   - Balance Type: `Ending Balance`
   - Sign Rule: `As-Is`
2. Save
3. Generate report or run Test Fetch

**Expected Result:**
- `BS_CASH_CY` = sum of EndingBalance for all accounts 10100–10199
- `BS_CASH_PY` = same for prior year period

**Pass/Fail:** ___
**Notes:** ___

---

### B-02: Single Account (From = To)
**Objective:** Verify single-account range works.

**Steps:**
1. Add line:
   - Account From: `10100`
   - Account To: `10100`
2. Run report

**Expected Result:**
- Only account 10100 summed — no adjacent accounts included

**Pass/Fail:** ___
**Notes:** ___

---

### B-03: Missing Account From
**Objective:** Verify AccountFrom is required for ACCOUNT lines.

**Steps:**
1. Add ACCOUNT line
2. Leave Account From blank
3. Fill Account To
4. Save

**Expected Result:**
- Error: *"Account From is required for Account Range line types."*

**Pass/Fail:** ___
**Notes:** ___

---

### B-04: Missing Account To
**Objective:** Verify AccountTo is required for ACCOUNT lines.

**Steps:**
1. Add ACCOUNT line
2. Fill Account From
3. Leave Account To blank
4. Save

**Expected Result:**
- Error: *"Account To is required for Account Range line types."*

**Pass/Fail:** ___
**Notes:** ___

---

### B-05: Account Type Filter = Asset
**Objective:** Verify only Asset-type accounts are summed when filter = A.

**Prerequisites:** Range `10000–99999` contains mixed account types.

**Steps:**
1. Add ACCOUNT line:
   - Account From: `10000`, Account To: `99999`
   - Account Type Filter: `Asset (A)`
2. Also create a second line with same range but no type filter
3. Compare results

**Expected Result:**
- Filtered line (Asset only) < unfiltered line total
- Only accounts where GI returns Type = `A` are included

**Pass/Fail:** ___
**Notes:** ___

---

### B-06: Account Type Filter = Liability
**Objective:** Verify only Liability accounts summed with positive presentation value.

**Steps:**
1. Add ACCOUNT line:
   - Account From: `20000`, Account To: `29999`
   - Account Type Filter: `Liability (L)`
   - Sign Rule: `As-Is`
2. Run report

**Expected Result:**
- Value is **positive** — engine normalizes L/I from credit (negative GL) to positive via `ApplyAccountTypeSign`
- Note: do NOT use Flip Sign on top of this unless intentionally wanting negative

**Pass/Fail:** ___
**Notes:** ___

---

### B-07: Account Type Filter = Blank (All Types)
**Objective:** Verify blank type filter includes all account types.

**Steps:**
1. Add ACCOUNT line with wide range, Account Type Filter = blank (All Types)
2. Run report

**Expected Result:**
- All account types (A, L, E, I) in range included
- Total equals sum of separate type-filtered lines

**Pass/Fail:** ___
**Notes:** ___

---

### B-08: Sign Rule = As-Is on Asset Account
**Objective:** Verify As-Is leaves asset value positive.

**Steps:**
1. Account range for assets (type A), Sign Rule = As-Is

**Expected Result:**
- Value is positive (assets stored as debit balance = positive after normalization)

**Pass/Fail:** ___
**Notes:** ___

---

### B-09: Sign Rule = Flip
**Objective:** Verify Flip multiplies final value by -1.

**Steps:**
1. Note value of a line with Sign Rule = As-Is
2. Change same line to Sign Rule = Flip
3. Run report

**Expected Result:**
- Value = negative of As-Is value
- Use case: presenting a contra-asset (e.g. accumulated depreciation) as negative

**Pass/Fail:** ___
**Notes:** ___

---

### B-10: Balance Type = Beginning Balance
**Objective:** Verify Beginning Balance uses prior fiscal year-end EndingBalance.

**Steps:**
1. Add ACCOUNT line, Balance Type = `Beginning Balance`
2. Run for period `12-2025`

**Expected Result:**
- Value = EndingBalance of the fiscal year-end period *before* 2025 (e.g. Dec 2024)
- NOT the EndingBalance of the current period

**Pass/Fail:** ___
**Notes:** ___

---

### B-11: Balance Type = Debit (YTD)
**Objective:** Verify YTD cumulative debit is used (not single period debit).

**Steps:**
1. Add ACCOUNT line, Balance Type = `Debit (YTD)`
2. Run for period `06-2025`

**Expected Result:**
- Value = sum of Debit from Jan 2025 to Jun 2025 (6 months cumulative)
- Must be >= single month's debit

**Pass/Fail:** ___
**Notes:** ___

---

### B-12: Balance Type = Credit (YTD)
**Objective:** Verify YTD cumulative credit.

**Steps:**
1. Balance Type = `Credit (YTD)`, run for Jun 2025

**Expected Result:**
- Sum of Credit Jan–Jun 2025

**Pass/Fail:** ___
**Notes:** ___

---

### B-13: Balance Type = Movement (YTD)
**Objective:** Verify YTD net movement = Debit YTD − Credit YTD.

**Steps:**
1. Balance Type = `Movement (YTD)`
2. Cross-check: Debit(YTD) − Credit(YTD) should equal Movement(YTD)

**Expected Result:**
- Movement = Debit YTD − Credit YTD for same account range and period

**Pass/Fail:** ___
**Notes:** ___

---

> **Removed.** B-14 (Period Debit), B-15 (Period Credit), B-16 (Period Movement) were dropped — the engine now exposes only **5 fiscal-year-to-date balance types** (`ENDING`, `BEGINNING`, `DEBIT`, `CREDIT`, `MOVEMENT`). Single-period (month-only) balance types no longer exist. For a month-only delta compute it inside the Word template (`{{PFX_X_CY}} − {{PFX_X_PY}}`); formulas inside a Definition cannot mix periods.

---

### B-17: Subaccount Filter — Matching Value
**Objective:** Verify SubaccountFilter restricts rows to exact subaccount match.

**Prerequisites:** Account range has rows with multiple subaccounts (e.g. `000`, `001`, `002`).

**Steps:**
1. Add ACCOUNT line with SubaccountFilter = `000`
2. Also add identical line without SubaccountFilter
3. Run report, compare results

**Expected Result:**
- Filtered line total < unfiltered total
- Only rows where Subaccount = `000` included in filtered line

**Pass/Fail:** ___
**Notes:** ___

---

### B-18: Subaccount Filter — No Match
**Objective:** Verify zero result when subaccount filter matches nothing.

**Steps:**
1. SubaccountFilter = `ZZZ` (value known not to exist)
2. Run report

**Expected Result:**
- Result = `0` (displayed as `-`)
- Trace log shows warning: `No match: range ... filters Sub='ZZZ'`
- No error thrown

**Pass/Fail:** ___
**Notes:** ___

---

### B-19: Branch Filter — Valid Branch
**Objective:** Verify BranchFilter restricts to specific branch rows.

**Prerequisites:** GL data exists for branches `HQ` and `BRANCH1`.

**Steps:**
1. Add ACCOUNT line, BranchFilter = `HQ`
2. Add identical line, BranchFilter = `BRANCH1`
3. Add identical line, no BranchFilter
4. Run report

**Expected Result:**
- HQ line + BRANCH1 line ≈ unfiltered line (may differ if more branches exist)
- Each filtered line < unfiltered

**Pass/Fail:** ___
**Notes:** ___

---

### B-20: Branch Filter — Non-Existent Branch
**Objective:** Verify graceful zero result for invalid branch.

**Steps:**
1. BranchFilter = `NONEXISTBRANCH`
2. Run report

**Expected Result:**
- Result = `0`
- No error — ValidateValue = false on selector means field accepts any string

**Pass/Fail:** ___
**Notes:** ___

---

### B-21: Organization Filter
**Objective:** Verify OrganizationFilter restricts to specific organization.

**Steps:**
1. Add ACCOUNT line, OrganizationFilter = `CENSOF` (or your org code)
2. Add identical line without filter
3. Run and compare

**Expected Result:**
- Filtered line ≤ unfiltered line
- Only rows matching OrganizationID included

**Pass/Fail:** ___
**Notes:** ___

---

### B-22: Ledger Filter = ACTUAL
**Objective:** Verify LedgerFilter excludes non-ACTUAL ledgers.

**Prerequisites:** Multiple ledgers exist (ACTUAL, BUDGET, STAT).

**Steps:**
1. Add ACCOUNT line, LedgerFilter = `ACTUAL`
2. Add identical line, no LedgerFilter
3. Compare

**Expected Result:**
- ACTUAL-filtered line may differ from unfiltered (if budget/stat entries exist)
- Only ACTUAL ledger rows included in filtered line

**Pass/Fail:** ___
**Notes:** ___

---

### B-23: All Dimension Filters Combined
**Objective:** Verify all four filters work together (AND logic).

**Steps:**
1. Set SubaccountFilter = `000`, BranchFilter = `HQ`, OrganizationFilter = `CENSOF`, LedgerFilter = `ACTUAL`
2. Run report

**Expected Result:**
- Only rows matching ALL four conditions included
- Result ≤ any single-filter result

**Pass/Fail:** ___
**Notes:** ___

---

### B-24: Dimension Filters Disabled for Non-ACCOUNT Types
**Objective:** Verify dimension filter fields are disabled for Subtotal/Calculated/Heading.

**Steps:**
1. Open an ACCOUNT line — verify SubaccountFilter, BranchFilter, OrganizationFilter, LedgerFilter are **enabled**
2. Change Line Type to `Subtotal` — verify all four fields are **disabled**
3. Change to `Calculated` — verify all four fields are **disabled**
4. Change to `Heading` — verify all four fields are **disabled**
5. Change back to `Account Range` — verify all four fields are **enabled** again

**Expected Result:**
- Enabled only for Account Range type

**Pass/Fail:** ___
**Notes:** ___

---

### B-25: Dimension Filters Auto-Cleared on Type Change
**Objective:** Verify dimension filter values are cleared when switching away from ACCOUNT.

**Steps:**
1. Set LineType = Account Range
2. Enter SubaccountFilter = `000`, BranchFilter = `HQ`, OrganizationFilter = `CENSOF`, LedgerFilter = `ACTUAL`
3. Change LineType to `Subtotal`
4. Observe filter field values

**Expected Result:**
- All four filter fields cleared to blank automatically

**Pass/Fail:** ___
**Notes:** ___

---

### B-26: Account Range — No Matching Accounts
**Objective:** Verify zero result for range with no GL data.

**Steps:**
1. Account From = `99900`, Account To = `99999` (unused range)
2. Run report

**Expected Result:**
- Result = `0` (displayed as `-`)
- No error

**Pass/Fail:** ___
**Notes:** ___

---

### B-27: Large Account Range
**Objective:** Verify engine handles full chart-of-accounts range.

**Steps:**
1. Account From = `10000`, Account To = `99999`
2. Run report

**Expected Result:**
- All accounts summed without error or timeout
- Result equals sum of all individual account lines

**Pass/Fail:** ___
**Notes:** ___

---

## SECTION C — SUBTOTAL Line Type

---

### C-01: Basic Subtotal
**Objective:** Verify Subtotal sums all children with matching ParentLineCode.

**Prerequisites:** ACCOUNT lines `CASH`, `AR`, `INVENTORY` exist in definition.

**Steps:**
1. Set ParentLineCode = `CURRENT_ASSETS` on `CASH`, `AR`, `INVENTORY`
2. Add new line:
   - Line Code: `CURRENT_ASSETS`
   - Line Type: `Subtotal`
3. Save and run report

**Expected Result:**
- `CURRENT_ASSETS = CASH + AR + INVENTORY`
- Cross-verify: manually add the three values

**Pass/Fail:** ___
**Notes:** ___

---

### C-02: Nested Subtotals
**Objective:** Verify topo sort handles multi-level subtotal hierarchy.

**Steps:**
1. Create:
   - `CASH` (Account) → ParentLineCode = `CURRENT_ASSETS`
   - `AR` (Account) → ParentLineCode = `CURRENT_ASSETS`
   - `FIXED_ASSETS` (Account) → ParentLineCode = `NON_CURRENT_ASSETS`
   - `CURRENT_ASSETS` (Subtotal) → ParentLineCode = `TOTAL_ASSETS`
   - `NON_CURRENT_ASSETS` (Subtotal) → ParentLineCode = `TOTAL_ASSETS`
   - `TOTAL_ASSETS` (Subtotal) — no parent
2. Deliberately set SortOrder so `TOTAL_ASSETS` appears before children
3. Run report

**Expected Result:**
- `TOTAL_ASSETS = CURRENT_ASSETS + NON_CURRENT_ASSETS`
- Topo sort resolves order correctly regardless of SortOrder

**Pass/Fail:** ___
**Notes:** ___

---

### C-03: Subtotal with No Children
**Objective:** Verify empty subtotal returns zero without error.

**Steps:**
1. Add Subtotal line `EMPTY_TOTAL`
2. No other lines reference it as parent
3. Run report

**Expected Result:**
- Result = `0` (displayed as `-`)
- No error

**Pass/Fail:** ___
**Notes:** ___

---

### C-04: Subtotal ParentLineCode Enabled
**Objective:** Verify Subtotal line can itself be a child of another Subtotal.

**Steps:**
1. `CURRENT_ASSETS` (Subtotal) → ParentLineCode = `TOTAL_ASSETS`
2. Verify ParentLineCode field is **editable** for Subtotal line type

**Expected Result:**
- ParentLineCode field enabled for Subtotal
- Nested hierarchy works (see C-02)

**Pass/Fail:** ___
**Notes:** ___

---

### C-05: Account Fields Disabled for Subtotal
**Objective:** Verify account-specific fields disabled when LineType = Subtotal.

**Steps:**
1. Set LineType = `Subtotal`
2. Observe: AccountFrom, AccountTo, AccountTypeFilter, SignRule, BalanceType

**Expected Result:**
- All five fields are **disabled** (grayed out)

**Pass/Fail:** ___
**Notes:** ___

---

### C-06: Subtotal Includes Calculated Child (New Feature)
**Objective:** Verify CALCULATED line with ParentLineCode is included in Subtotal.

**Steps:**
1. Create:
   - `NET_PROFIT` (Calculated), Formula = `REVENUE - OPEX`, ParentLineCode = `TOTAL_RESERVES`
   - `RETAINED_EARNINGS` (Account), ParentLineCode = `TOTAL_RESERVES`
   - `TOTAL_RESERVES` (Subtotal)
2. Run report

**Expected Result:**
- `TOTAL_RESERVES = NET_PROFIT + RETAINED_EARNINGS`
- NET_PROFIT (calculated) included in subtotal sum

**Pass/Fail:** ___
**Notes:** ___

---

## SECTION D — CALCULATED Line Type

---

### D-01: Basic Arithmetic Formula
**Objective:** Verify +, -, *, / operators work in formulas.

**Steps:**
1. Create:
   - `REVENUE` (Account), account range for income accounts
   - `COGS` (Account), account range for cost accounts
   - `GROSS_PROFIT` (Calculated), Formula = `REVENUE - COGS`
2. Run report

**Expected Result:**
- `GROSS_PROFIT = REVENUE - COGS` (verify manually)
- Both CY and PY values calculated

**Pass/Fail:** ___
**Notes:** ___

---

### D-02: Formula with Parentheses
**Objective:** Verify operator precedence and parentheses.

**Steps:**
1. Formula = `(REVENUE - COGS) / REVENUE`
2. Verify result = gross margin ratio

**Expected Result:**
- Result respects parentheses: (REVENUE - COGS) computed first, then divided
- Without parentheses `REVENUE - COGS / REVENUE` would give different result

**Pass/Fail:** ___
**Notes:** ___

---

### D-03: Formula Referencing Subtotal
**Objective:** Verify formula can reference Subtotal lines (topo sort ensures subtotal resolved first).

**Steps:**
1. `TOTAL_ASSETS` (Subtotal) exists
2. `TOTAL_LIAB` (Subtotal) exists
3. `NET_ASSETS` (Calculated), Formula = `TOTAL_ASSETS - TOTAL_LIAB`
4. Deliberately set SortOrder of `NET_ASSETS` before `TOTAL_ASSETS`
5. Run report

**Expected Result:**
- `NET_ASSETS` calculated correctly despite SortOrder — topo sort processes subtotals first

**Pass/Fail:** ___
**Notes:** ___

---

### D-04: Cross-Definition Reference — Explicit Prefix
**Objective:** Verify formula can reference lines from another definition using explicit prefix.

**Prerequisites:** Two definitions linked to same report — `BS` (Balance Sheet) and `PL` (Profit & Loss).

**Steps:**
1. In a third definition (e.g. `CF`), add Calculated line:
   - Formula = `BS_TOTAL_ASSETS - PL_NET_PROFIT`
2. Run report

**Expected Result:**
- `BS_TOTAL_ASSETS` resolved from BS definition
- `PL_NET_PROFIT` resolved from PL definition
- Result = correct cross-definition arithmetic

**Pass/Fail:** ___
**Notes:** ___

---

### D-05: Cross-Definition Reference — Implicit Prefix (Own Definition)
**Objective:** Verify unqualified token resolves to own definition.

**Steps:**
1. In `BS` definition, formula = `TOTAL_ASSETS - TOTAL_LIAB` (no prefix)
2. Run report

**Expected Result:**
- Resolved as `BS_TOTAL_ASSETS - BS_TOTAL_LIAB`
- Trace log shows implicit resolution

**Pass/Fail:** ___
**Notes:** ___

---

### D-06: Calculated Line in Subtotal
**Objective:** Verify CALCULATED line with ParentLineCode included in parent Subtotal (covered also in C-06).

**Steps:** See C-06.

**Pass/Fail:** ___
**Notes:** ___

---

### D-07: Missing Formula Validation
**Objective:** Verify formula is required for Calculated lines.

**Steps:**
1. Add line, LineType = Calculated
2. Leave Formula blank
3. Save

**Expected Result:**
- Error: *"Formula is required for Calculated line types."*

**Pass/Fail:** ___
**Notes:** ___

---

### D-08: Formula References Unknown LineCode
**Objective:** Verify an unresolved formula reference fails the run loudly (does not silently zero).

**Steps:**
1. Formula = `NONEXISTENT_CODE + REVENUE`
2. Run report

**Expected Result:**
- The engine **throws** `PXException` with message *"Formula references unknown Line Code 'BS_NONEXISTENT_CODE'. Ensure it is defined with a lower Sort Order."* (`Messages.UnknownFormulaLineCode`).
- Generation **fails** — the record lands on Status `Failed`. The value does **not** default to `0`, and no partial/zeroed document is produced.

**Pass/Fail:** ___
**Notes:** ___

---

### D-09: Division by Zero in Formula
**Objective:** Verify division by zero is handled gracefully.

**Steps:**
1. Ensure a line `ZERO_LINE` resolves to `0`
2. Formula = `REVENUE / ZERO_LINE`
3. Run report

**Expected Result:**
- Result = `0`
- Trace log shows warning: `Division by zero in formula — result set to 0.`
- No exception

**Pass/Fail:** ___
**Notes:** ___

---

### D-10: Formula Field Disabled for Non-Calculated Types
**Objective:** Verify Formula field is only editable for Calculated type.

**Steps:**
1. Set LineType = Account Range → verify Formula is **disabled**
2. Set LineType = Subtotal → verify Formula is **disabled**
3. Set LineType = Heading → verify Formula is **disabled**
4. Set LineType = Calculated → verify Formula is **enabled**

**Expected Result:**
- Formula enabled only when LineType = Calculated

**Pass/Fail:** ___
**Notes:** ___

---

### D-11: Formula Cleared on LineType Change Away from Calculated
**Objective:** Verify formula value is cleared when switching to non-Calculated type.

**Steps:**
1. LineType = Calculated, Formula = `REVENUE - COGS`
2. Change LineType to `Account Range`
3. Observe Formula field

**Expected Result:**
- Formula field cleared to blank automatically

**Pass/Fail:** ___
**Notes:** ___

---

### D-12: ParentLineCode Enabled for Calculated
**Objective:** Verify ParentLineCode is editable for Calculated lines (new feature).

**Steps:**
1. Set LineType = Calculated
2. Observe ParentLineCode field

**Expected Result:**
- ParentLineCode field is **enabled** — can assign group

**Pass/Fail:** ___
**Notes:** ___

---

### D-13: Circular Dependency Detection
**Objective:** Verify circular formula references are caught.

**Steps:**
1. `LINE_A` (Calculated), Formula = `LINE_B`
2. `LINE_B` (Calculated), Formula = `LINE_A`
3. Run report

**Expected Result:**
- Error thrown: *"Circular dependency detected in report definitions. The following line codes form a cycle and cannot be resolved: BS_LINE_A, BS_LINE_B. Please revise the formulas to break the cycle."*
- Report generation does not proceed; record lands on `Failed`

**Pass/Fail:** ___
**Notes:** ___

---

## SECTION E — HEADING Line Type

---

### E-01: Heading Line Renders Empty
**Objective:** Verify Heading line produces no value in output.

**Steps:**
1. Add line:
   - Line Code: `HDG_ASSETS`
   - Line Type: `Heading`
   - Description: `ASSETS`
2. Run report

**Expected Result:**
- `BS_HDG_ASSETS_CY` and `BS_HDG_ASSETS_PY` = empty string in Word template
- No numeric value produced

**Pass/Fail:** ___
**Notes:** ___

---

### E-02: Heading Fields Disabled
**Objective:** Verify all value-related fields disabled for Heading.

**Steps:**
1. Set LineType = Heading
2. Observe field states

**Expected Result:**
- AccountFrom, AccountTo, AccountTypeFilter, SignRule, BalanceType → **disabled**
- Formula → **disabled**
- ParentLineCode → **disabled**
- IsVisible → **disabled**
- All dimension filters → **disabled**

**Pass/Fail:** ___
**Notes:** ___

---

### E-03: IsVisible Auto-Set to False for Heading
**Objective:** Verify IsVisible automatically set false when switching to Heading.

**Steps:**
1. Line with IsVisible = true
2. Change LineType to Heading
3. Observe IsVisible

**Expected Result:**
- IsVisible automatically set to `false`

**Pass/Fail:** ___
**Notes:** ___

---

### E-04: ParentLineCode Auto-Cleared for Heading
**Objective:** Verify ParentLineCode cleared when switching to Heading.

**Steps:**
1. Set ParentLineCode = `TOTAL_ASSETS`
2. Change LineType to Heading
3. Observe ParentLineCode

**Expected Result:**
- ParentLineCode cleared to blank

**Pass/Fail:** ___
**Notes:** ___

---

## SECTION F — IsVisible

---

### F-01: IsVisible = True
**Objective:** Verify visible line produces formatted value in output.

**Steps:**
1. Line with IsVisible = true, value = 1,234,567
2. Run report

**Expected Result:**
- Placeholder in Word = `1,234,567` (or formatted per rounding settings)

**Pass/Fail:** ___
**Notes:** ___

---

### F-02: IsVisible = False
**Objective:** Verify hidden line produces empty string in output.

**Steps:**
1. Line with IsVisible = false
2. Run report

**Expected Result:**
- Placeholder in Word = empty string
- Line still calculated internally (used by other formulas)

**Pass/Fail:** ___
**Notes:** ___

---

### F-03: Hidden Line Used in Formula
**Objective:** Verify hidden intermediate line still contributes correct value to formulas.

**Steps:**
1. `GROSS_PROFIT` (Calculated), Formula = `REVENUE - COGS`, IsVisible = false
2. `NET_PROFIT` (Calculated), Formula = `GROSS_PROFIT - OPEX`, IsVisible = true
3. Run report

**Expected Result:**
- `BS_GROSS_PROFIT_CY` = empty in Word
- `BS_NET_PROFIT_CY` = correct value (GROSS_PROFIT used internally)

**Pass/Fail:** ___
**Notes:** ___

---

### F-04: IsVisible Disabled for Heading
**Objective:** Already verified in E-02. Cross-reference.

**Pass/Fail:** ___
**Notes:** ___

---

## SECTION G — Validations & Edge Cases

---

### G-01: Line Code Required
**Objective:** Verify LineCode is enforced.

**Steps:**
1. Add line, leave Line Code blank
2. Save

**Expected Result:**
- Error: *"Line Code is required."*

**Pass/Fail:** ___
**Notes:** ___

---

### G-02: Duplicate Line Code Within Definition
**Objective:** Verify duplicate LineCode within same definition rejected.

**Steps:**
1. Add two lines both with LineCode = `CASH`
2. Save

**Expected Result:**
- Error: *"Line Code must be unique within the same definition."*

**Pass/Fail:** ___
**Notes:** ___

---

### G-03: Same Line Code in Different Definitions
**Objective:** Verify LineCode uniqueness is scoped per definition.

**Steps:**
1. Definition `BS` has LineCode `CASH`
2. Definition `PL` also has LineCode `CASH`
3. Save both

**Expected Result:**
- No error — scoped per definition, disambiguated by prefix (`BS_CASH` vs `PL_CASH`)

**Pass/Fail:** ___
**Notes:** ___

---

### G-04: Sort Order Drives Display Sequence
**Objective:** Verify lines display in SortOrder sequence.

**Steps:**
1. Add lines with SortOrder: 30, 10, 20 (out of order)
2. Save, reopen definition

**Expected Result:**
- Lines displayed in order: 10, 20, 30
- Calculation topo sort independent of display order

**Pass/Fail:** ___
**Notes:** ___

---

> **Removed.** G-05 (Copy Definition action) was dropped — the **Copy Definition** toolbar action no longer exists on FR101002. To clone a definition, use the standard Acumatica `Copy/Paste record` toolbar buttons and rename Definition Code + Prefix on the new record.

---

### G-06: Zero Value Formatting
**Objective:** Verify zero values display as `-` not `0`.

**Steps:**
1. Create line for account range with no GL data
2. Run report

**Expected Result:**
- Placeholder = `-` (dash, not `0`)

**Pass/Fail:** ___
**Notes:** ___

---

### G-07: Negative Value Formatting
**Objective:** Verify negative values display in parentheses.

**Steps:**
1. Create line that produces a negative result (e.g. Flip Sign on an asset with positive balance)
2. Run report

**Expected Result:**
- Placeholder = `(1,234,567)` — negative shown in parentheses, not with minus sign

**Pass/Fail:** ___
**Notes:** ___

---

### G-08: Multi-Definition Report — Prefix Uniqueness Enforced
**Objective:** Verify two linked definitions cannot share same prefix in a report.

**Steps:**
1. Open a Financial Report (FR101000)
2. Add Definition Link for `BS` (prefix BS)
3. Add another Definition Link for a definition that also has prefix `BS`
4. Save

**Expected Result:**
- Error: *"Prefix 'BS' is already used by another linked definition in this report. Each definition must have a unique prefix."*

**Pass/Fail:** ___
**Notes:** ___

---

### G-09: Duplicate Global Key Detection
**Objective:** Verify error when two definitions produce same GlobalKey (PREFIX_LINECODE).

**Steps:**
1. Two definitions both with Prefix `BS` linked to same report (should be blocked by G-08)
2. Alternatively: two lines in same definition with same LineCode (blocked by G-02)

**Expected Result:**
- Either blocked at save (G-02, G-08) or caught at calculation time with error: *"Duplicate line code(s) detected: BS_CASH. Each definition prefix and line code combination must be unique across all linked definitions."*

**Pass/Fail:** ___
**Notes:** ___

---

## SECTION H — Full Balance Sheet Scenario

---

### H-01: Complete Balance Sheet Structure
**Objective:** End-to-end test of a complete Balance Sheet definition.

**Steps:**
1. Create definition `FULL-BS`, Prefix = `BS`
2. Add lines in this order (SortOrder 10–120):

| Sort | LineCode | Type | AccountFrom | AccountTo | Parent | Formula |
|------|----------|------|-------------|-----------|--------|---------|
| 10 | HDG_ASSETS | Heading | — | — | — | — |
| 20 | CASH | Account | 10100 | 10199 | CURRENT_ASSETS | — |
| 30 | AR | Account | 11000 | 11999 | CURRENT_ASSETS | — |
| 40 | INVENTORY | Account | 12000 | 12999 | CURRENT_ASSETS | — |
| 50 | CURRENT_ASSETS | Subtotal | — | — | TOTAL_ASSETS | — |
| 60 | FIXED_ASSETS | Account | 15000 | 15999 | NON_CURRENT_ASSETS | — |
| 70 | ACCUM_DEPR | Account | 16000 | 16999 | NON_CURRENT_ASSETS | — |
| 80 | NON_CURRENT_ASSETS | Subtotal | — | — | TOTAL_ASSETS | — |
| 90 | TOTAL_ASSETS | Subtotal | — | — | — | — |
| 100 | HDG_LIAB | Heading | — | — | — | — |
| 110 | AP | Account | 20000 | 20999 | TOTAL_LIAB | — |
| 120 | TOTAL_LIAB | Subtotal | — | — | — | — |
| 160 | NET_ASSETS | Calculated | — | — | — | `TOTAL_ASSETS - TOTAL_LIAB` |

3. Run report for a valid period

**Expected Result:**
- All lines calculate correctly
- `HDG_ASSETS` and `HDG_LIAB` = empty
- CY and PY both produced for all lines

**Pass/Fail:** ___
**Notes:** ___

---

## Test Execution Summary

| Section | Total Tests | Passed | Failed | Skipped |
|---------|-------------|--------|--------|---------|
| A — Definition Header | 9 | | | |
| B — Account Line Type | 24 | | | |
| C — Subtotal Line Type | 6 | | | |
| D — Calculated Line Type | 13 | | | |
| E — Heading Line Type | 4 | | | |
| F — IsVisible | 4 | | | |
| G — Validations & Edge Cases | 8 | | | |
| H — Full Scenario | 1 | | | |
| **TOTAL** | **69** | | | |

> A-10, B-14, B-15, B-16, G-05 removed in v2.1.3 — see inline notes above. Original IDs are preserved (no renumbering) so test logs from older runs stay traceable.

---

**Tested by:** _______________
**Date:** _______________
**Build/CP Version:** _______________
