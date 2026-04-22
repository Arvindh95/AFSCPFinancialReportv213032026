# Step 1: Tenant Credentials Setup (FR101001)

**Screen:** AFS > Tenant Credentials
**Screen ID:** FR101001
**Purpose:** Store API credentials so the system can connect to Acumatica's OData API and the Gamma presentation API.

---

## What You Need Before Starting

- Your Acumatica instance URL (e.g., `http://localhost/2025R2`)
- A Connected Application configured in Acumatica (System > Integration > Connected Applications)
- An API user account with access to the Generic Inquiries you plan to use
- (Optional) A Gamma API key from [gamma.app](https://gamma.app) if you plan to generate presentations

---

## Step-by-Step

### 1. Navigate to the Screen

Go to **AFS > Tenant Credentials** in the left menu, or type `FR101001` in the search bar.

### 2. Add a New Row

Click the **+** button in the toolbar to add a new row.

### 3. Fill in Each Column

| Column               | What to Enter                                                                    | Example                     |
| -------------------- | -------------------------------------------------------------------------------- | --------------------------- |
| Tenant Name          | Your Acumatica tenant name. Must match the tenant in the URL.                    | `SalesDemo`               |
| Base URL             | Root URL of your Acumatica instance. No trailing slash.                          | `http://localhost/2025R2` |
| Company Number       | The internal company number. Find it in System > Manage > Companies.             | `2`                       |
| Client ID            | OAuth2 Client ID from your Connected Application.                                | `A1B2C3D4-E5F6-...`       |
| Client Secret        | OAuth2 Client Secret from your Connected Application.                            | `secretvalue123`          |
| Username             | API user account username.                                                       | `apiuser`                 |
| Password             | API user account password.                                                       | `P@ssw0rd`                |
| Presentation API Key | Gamma API key. Only needed for FR101003. Leave blank if not using presentations. | `gamma_key_abc123`        |

### 4. Save

Click **Save** in the toolbar.

---

## How to Get a Connected Application (Client ID / Client Secret)

1. In Acumatica, go to **System > Integration > Connected Applications**
2. Click **+** to create a new application
3. Set:
   - Application Name: `AFS Financial Report`
   - OAuth 2.0 Flow: `Resource Owner Password Credentials`
4. Save — the system generates a Client ID and Client Secret
5. Copy both values into FR101001

## Security Notes

- All credential fields are encrypted at rest using Acumatica's RSA encryption
- Credentials are cached in memory for 10 minutes during report generation
- The cache is cleared automatically after each generation cycle
- Always use HTTPS for the Base URL in production

---

## Validation Rules

- Company Number is required
- Tenant Name is required and must be unique across all rows
- If any required field is missing, the save will fail with an error message

---

## Example: Complete Row

| Tenant Name | Base URL                | Company Number | Client ID | Client Secret | Username | Password | Presentation API Key |
| ----------- | ----------------------- | -------------- | --------- | ------------- | -------- | -------- | -------------------- |
| SalesDemo   | http://localhost/2025R2 | 2              | ********  | ********      | ******** | ******** | ********             |

(Values are masked after save for security)
