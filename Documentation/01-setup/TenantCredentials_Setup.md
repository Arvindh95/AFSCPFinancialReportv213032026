# Tenant Credentials Setup — FR101001

This document describes how to store the **API credentials** that the AFS Financial Report module uses to read GL data from Acumatica's OData layer and (optionally) generate presentations through the Gamma API. One row per tenant — the engine looks up credentials by `Company Number` at run time.

> **Screen ID:** FR101001
> **Screen Title:** AFS Credentials Config
> **Menu path:** *AFS → Configuration → AFS Credentials Config*

---

## Prerequisites

- Acumatica 2025 R2 with the AFSCPFinancialReport customization published.
- A **Connected Application** already created in Acumatica — see [ConnectedApplication_Setup.md](ConnectedApplication_Setup.md). You will need its **Client ID** and **Client Secret**.
- An Acumatica user account with access to the Generic Inquiries the report definitions read from (typically `AFS-Trial-Balance`).
- The **Company Number** for the tenant. Find it in *System → Manage → Companies*; it is the integer key, not the tenant name.
- *(Optional)* A Gamma API key from [gamma.app](https://gamma.app), only needed if you plan to use the [MBR Report Generation screen (FR101003)](../02-generation/MBRReport_Generation.md).

---

## Steps

### Step 1 — Navigate to AFS Credentials Config

1. Log in to Acumatica.
2. In the top search bar, type **AFS Credentials** and select **AFS Credentials Config** under *AFS → Configuration*.

![Top search showing AFS Credentials Config result](../images/tenant_credentials/tenantcreds_02_search.png)

The screen opens in grid mode. Each existing tenant occupies one row. Sensitive columns (Client ID, Client Secret, Username, Password, Presentation API Key) display as `********` — they are encrypted at rest with `PXRSACryptString` and never re-shown in plaintext after save.

![FR101001 landing — existing SalesDemo row with masked fields](../images/tenant_credentials/tenantcreds_01_landing.png)

The toolbar carries the standard Acumatica grid actions:

| Icon | Tooltip       | Purpose                                              |
| ---- | ------------- | ---------------------------------------------------- |
| ↻    | Refresh       | Reload data from the database.                       |
| ↶    | Cancel (Esc)  | Discard all unsaved changes in the grid.             |
| +    | Add Row       | Insert a new empty row at the bottom.                |
| ×    | Delete        | Delete the currently selected row.                   |
| Save | Save          | Persist all pending inserts / edits / deletes.       |
| ⊢    | Adjust Columns| Auto-fit column widths.                              |
| ⊠    | Export to Excel| Export the grid contents.                           |

---

### Step 2 — Add a New Row

Click the **+** (Add Row) button on the toolbar. An empty row appears at the bottom of the grid with focus in the **Tenant Name** cell.

![Add Row tooltip + empty editable row inserted](../images/tenant_credentials/tenantcreds_03_new_row.png)

> The grid edits in place — there is no separate detail pane. Use **Tab** to move between cells.

---

### Step 3 — Fill the 8 Columns

Enter values for every column. Five of the eight are RSA-encrypted on save; once you tab out of an encrypted cell the value is masked as `********`, but it is committed to the row buffer correctly — Save persists it.

| #  | Column                  | Required | Encrypted | Example                                  |
| -- | ----------------------- | -------- | --------- | ---------------------------------------- |
| 1  | **Tenant Name**         | yes      | no        | `SalesDemo`                              |
| 2  | **Base URL**            | yes      | no        | `http://localhost/2025R2`                |
| 3  | **Company Number**      | yes (key)| no        | `2`                                      |
| 4  | **Client ID**           | yes      | yes       | `B8224E7A-0203-4DF4-B12B-7990E65C4524@SalesDemo` |
| 5  | **Client Secret**       | yes      | yes       | *(value copied once from Connected App popup)* |
| 6  | **Username**            | yes      | yes       | `admin`                                  |
| 7  | **Password**            | yes      | yes       | *(API user's password)*                  |
| 8  | **Presentation API Key**| optional | yes       | `gamma_xyz…` *(only needed for FR101003)*|

**Field rules**

- **Tenant Name** must match the tenant segment in the Acumatica URL exactly. It also appears as the suffix of the Connected Application's Client ID (`{GUID}@{TenantName}`).
- **Base URL** is the root of your Acumatica instance with **no trailing slash**. Use HTTPS in production.
- **Company Number** is the primary key of this row. Each tenant maps to exactly one row; saving two rows with the same Company Number fails on the unique key.
- **Client ID** / **Client Secret** come from the Connected Application created in [ConnectedApplication_Setup.md](ConnectedApplication_Setup.md) — paste the full values without trimming.
- **Username** / **Password** are an Acumatica user account with access to the Generic Inquiries the report definitions read.
- **Presentation API Key** is the Gamma API key. Leave blank unless using FR101003 — the Financial Report screen (FR101000) does not need it.

After filling, the row looks like the screenshot below — non-encrypted fields show plain values; encrypted fields already mask once focus leaves the cell:

![Filled row — Tenant Name, Base URL, Company Number visible; encrypted fields masked](../images/tenant_credentials/tenantcreds_04_filled_row.png)

---

### Step 4 — Save

Click **Save** in the toolbar (or press `Ctrl+S`). The graph runs validation and persists the row:

- **Tenant Name** must be unique across all rows (`Messages.TenantNameMustBeUnique`).
- **Company Number** must be set (`Messages.CompanyNumRequired`).

If both pass, the encrypted fields are written to the database via `PXRSACryptString` and the row reloads showing all five sensitive columns as `********`. The saved row matches the existing **SalesDemo** example shown in Step 1.

> **Important.** The plaintext you typed is **not retrievable** after save. If you need to rotate a credential, paste a new value into the masked cell and Save again — Acumatica replaces the encrypted blob.

---

### Step 5 — *(Optional)* Discard Without Saving

If you started a new row by mistake, click the **Cancel (Esc)** button on the toolbar (curved-arrow icon). All pending inserts / edits / deletes are reverted; the grid returns to its last-saved state.

![Cancel (Esc) toolbar button — discard pending changes](../images/tenant_credentials/tenantcreds_05_after_cancel.png)

---

## How the Engine Uses This Row

When **Generate Report** runs on FR101000:

1. The graph reads the report record's `CompanyID` (the Acumatica company the record was created under).
2. `MapCompanyIDToTenantName` looks up the matching `FLRTTenantCredentials` row by `CompanyNum` and returns its `TenantName`.
3. `CredentialProvider.GetCredentials(tenantName)` decrypts the row and returns a strongly-typed `AcumaticaCredentials` object holding the Base URL, Client ID, Client Secret, Username, and Password.
4. `AuthService` performs the OAuth2 *Resource Owner Password Credentials* flow against `{BaseURL}/identity/connect/token` and caches the bearer token for the duration of the run.
5. `FinancialDataService` uses the bearer token to fetch the Generic Inquiry rows over OData.

If the row is missing or the Company Number doesn't match, the run fails immediately with `Messages.NoTenantMapping` — *"Tenant mapping not found."*

---

## Field Reference (DAC)

| DAC field         | Display name           | Type                       | Notes                                              |
| ----------------- | ---------------------- | -------------------------- | -------------------------------------------------- |
| `CompanyNum`      | Company Number         | `PXDBInt` (key)            | Primary key. Maps to Acumatica `CompanyID`.        |
| `TenantName`      | Tenant Name            | `PXDBString(50)`           | Plain text. Used to scope OAuth login.             |
| `BaseURL`         | Base URL               | `PXDBString(255)`          | Plain text.                                        |
| `ClientIDNew`     | Client ID              | `PXRSACryptString`         | Encrypted at rest.                                 |
| `ClientSecretNew` | Client Secret          | `PXRSACryptString`         | Encrypted at rest.                                 |
| `UsernameNew`     | Username               | `PXRSACryptString`         | Encrypted at rest.                                 |
| `PasswordNew`     | Password               | `PXRSACryptString`         | Encrypted at rest.                                 |
| `GammaApiKey`     | Presentation API Key   | `PXRSACryptString`         | Encrypted at rest. Optional — FR101003 only.       |

> The `*New` suffix on the encrypted fields is a legacy artefact from a schema migration; the UI just labels them **Client ID** / **Client Secret** / **Username** / **Password**.

---

## Security Notes

- All five sensitive fields use `PXRSACryptString` — the plaintext only exists in memory during the request that wrote it; the column on disk holds an RSA-encrypted blob keyed off the Acumatica web.config machine key.
- `CredentialProvider` caches decrypted values per tenant for the duration of a single report run, then drops the reference at job exit so the values are not retained between runs.
- Always use HTTPS for the Base URL in any non-local environment — the OAuth flow sends Username and Password in the request body.
- This row is the *only* place the Acumatica password and the Gamma API key live in the customization database. Treat the row as sensitive and restrict screen rights on FR101001 accordingly (the SiteMap node ships with admin-only rights — see `_project/ScreenWithRights_FR101001.xml`).

---

## Validation Errors You May See

| Error                                | Cause                                                                                  | Fix                                                              |
| ------------------------------------ | -------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| `Tenant Name must be unique.`        | Two rows share the same Tenant Name.                                                   | Pick a unique name or edit the existing row instead of inserting.|
| `Company Number is required.`        | Saved a row with the Company Number cell blank.                                        | Fill the Company Number from *System → Manage → Companies*.      |
| `Tenant mapping not found.`          | Generated a report under a CompanyID with no matching `CompanyNum` row in this screen. | Add a row for that CompanyID, or move the report record to a tenant that already has credentials. |
| `Failed to authenticate.`            | Wrong Client ID / Client Secret / Username / Password, or Connected Application's Flow is not *Resource Owner Password Credentials*. | Re-paste the credentials from the Connected Applications screen. |

---

## Next Step

Proceed to [ReportDefinition_Setup.md](ReportDefinition_Setup.md) to create the Report Definition that drives placeholder calculation, then to [FinancialReport_Generation.md](../02-generation/FinancialReport_Generation.md) to produce the actual `.docx`.
