using PX.Common;

namespace FinancialReport
{
    [PXLocalizable]
    public static class Messages
    {
        // ==================================================
        // AUTHENTICATION & API MESSAGES
        // ==================================================
        public const string FailedToAuthenticate = "Failed to authenticate";
        public const string InvalidCredentials = "Failed to authenticate. Please check credentials.";
        public const string AccessTokenNotFound = "Access token not found in response.";
        public const string TokenExpirationNotFound = "Token expiration not found in response.";
        public const string FailedToRefreshToken = "Failed to refresh token";

        // ==================================================
        // API & DATA RETRIEVAL MESSAGES  
        // ==================================================
        public const string FailedToFetchOData = "Failed to fetch OData";
        public const string PutRequestFailed = "PUT request failed: {0}";
        public const string NoAPIFound = "No API credentials found for company";
        public const string NoCompanyIDFound = "No CompanyID found for ReportID {0}.";

        // ==================================================
        // FILE & TEMPLATE MESSAGES
        // ==================================================
        public const string NoteIDIsNull = "NoteID is null.";
        public const string NoFilesAssociated = "No files are associated with this record.";
        public const string TemplateHasNoFiles = "The selected template does not have any attached files.";
        public const string TemplateFileIsEmpty = "The selected template file is empty or could not be retrieved.";
        public const string FailedToRetrieveFile = "Failed to retrieve the file content.";
        public const string UnableToSaveGeneratedFile = "Unable to save the generated file.";

        // ==================================================
        // REPORT GENERATION MESSAGES
        // ==================================================
        public const string PleaseSelectTemplate = "Please select a template to generate the report.";
        public const string NoGeneratedFile = "No generated file is available for download. Please generate the report first.";
        public const string FileGenerationInProgress = "A report generation process is already running for this template.";
        public const string CurrentYearNotSpecified = "Current Year is not specified for the selected report.";
        public const string NoRecordIsSelected = "No record is selected";
        public const string NoReportSelected = "No report selected or report ID is missing.";
        public const string ReportIDNull = "ReportID cannot be null when retrieving CompanyID.";

        // ==================================================
        // USER INPUT VALIDATION MESSAGES
        // ==================================================
        public const string PleaseSelectABranch = "Please select a Branch";
        public const string PleaseSelectALedger = "Please select a Ledger";
        public const string FailedToSelectBranchorOrg = "Please select a Branch or Organization";

        // ==================================================
        // COMPANY & TENANT CONFIGURATION MESSAGES
        // ==================================================
        public const string CompanyNumRequired = "Company Number is required.";
        public const string TenantNameRequired = "Tenant Name is required.";
        public const string UnabletoDetermineCompany = "Unable to determine the current company.";
        public const string TenantMissingFromConfig = "Tenant mapping is missing in Web.config.";
        public const string TenantMissingFromDatabase = "Tenant mapping is missing in Database";
        public const string NoTenantMapping = "Tenant mapping not found.";
        public const string NoCalculation = "No placeholder logic configured for this tenant.";

        // ==================================================
        // CONFIGURATION & SYSTEM MESSAGES
        // ==================================================
        public const string MissingConfig = "Missing Config. Check Web Configurations";
        public const string UnselectedResetStatus = "Please select a report to reset.";

        // ==================================================
        // EXISTING CONSTANTS
        // ==================================================
        public const string TenantNameMustBeUnique = "Tenant Name must be unique.";
        public const string FailedToSaveMessage = "Failed to save: {0}";
        public const string TooManyPlaceholders = "Template contains {0} placeholders. Maximum allowed is {1}. Please simplify your template or split it into multiple reports.";
        public const string ReportGenerationTimeout = "Report generation timed out after {0} minutes. Please check template complexity or contact support.";
        public const string WordDocumentMainPartNull = "Word document main part is null.";

        // ==================================================
        // REPORT DEFINITION MESSAGES
        // ==================================================
        public const string DefinitionCodeRequired = "Definition Code is required.";
        public const string DefinitionCodeMustBeUnique = "Definition Code must be unique.";
        public const string DefinitionPrefixRequired = "Definition Prefix is required. Enter a short alphanumeric code (e.g. BS, PL, CF).";
        public const string DefinitionPrefixMustBeAlphanumeric = "Definition Prefix must contain letters and digits only — no spaces, underscores, or special characters.";
        public const string DefinitionPrefixMustBeUnique = "Definition Prefix must be unique across all definitions. Another definition already uses this prefix.";
        public const string LineCodeRequired = "Line Code is required.";
        public const string LineCodeMustBeUnique = "Line Code must be unique within the same definition.";
        public const string AccountFromRequired = "Account From is required for Account Range line types.";
        public const string AccountToRequired = "Account To is required for Account Range line types.";
        public const string FormulaRequired = "Formula is required for Calculated line types.";
        public const string ConfirmCopyDefinition = "Copy this Report Definition and all its line items?";
        public const string NoDefinitionLineItems = "Report Definition has no line items configured. Please set up line items in the Report Definition screen.";
        public const string FormulaEvaluationFailed = "Formula evaluation failed for line '{0}': {1}";
        public const string UnknownFormulaLineCode = "Formula references unknown Line Code '{0}'. Ensure it is defined with a lower Sort Order.";

        // ==================================================
        // MULTI-DEFINITION REPORT MESSAGES
        // ==================================================
        public const string DefinitionRequired = "Please select a Report Definition.";
        public const string DuplicatePrefixInReport = "Prefix '{0}' is already used by another linked definition in this report. Each definition must have a unique prefix.";
        public const string CircularDependencyDetected = "Circular dependency detected in report definitions. The following line codes form a cycle and cannot be resolved: {0}. Please revise the formulas to break the cycle.";
        public const string DuplicateGlobalKey = "Duplicate line code(s) detected: {0}. Each definition prefix and line code combination must be unique across all linked definitions.";

        // ==================================================
        // SLIDE / PRESENTATION GENERATION MESSAGES
        // ==================================================
        public const string SlideGenerationInProgress = "A presentation generation process is already running for this report.";
        public const string PresentationTitleRequired = "Please enter a Presentation Title before generating a presentation.";
        public const string NoDefinitionsLinked = "No report definitions linked. Please add at least one definition on the Report Definitions tab before generating a presentation.";
        public const string VisibleLineItemsMissingDescriptions = "The following visible line items are missing descriptions: {0}. Please fill in all descriptions before generating a presentation.";
        public const string GammaApiKeyNotConfigured = "Presentation API Key is not configured. Please enter your API Key in the Tenant Credentials screen.";
        public const string NoGeneratedPresentation = "No presentation is available for download. Please generate a presentation first.";
        public const string MarkdownPreviewComplete = "Markdown preview saved. Download it from the Files panel (paperclip icon).";

        // ==================================================
        // GI & COLUMN MAPPING MESSAGES
        // ==================================================
        public const string GINameRequired = "Generic Inquiry Name is required to detect columns.";
        public const string NoColumnsDetected = "No columns were detected from the specified Generic Inquiry. Verify the GI name and that it returns data.";
        public const string FailedToDetectColumns = "Failed to detect columns from GI '{0}': {1}";
    }
}
