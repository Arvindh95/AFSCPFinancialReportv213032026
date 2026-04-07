-- =============================================
-- Full Project SQL: AFSCPFinancialReport
-- Creates ALL project tables from scratch.
-- Run this ONCE on a clean database.
-- Idempotent: safe to re-run (all blocks are guarded).
-- =============================================

PRINT '======================================================'
PRINT 'AFSCPFinancialReport - Full Schema Creation'
PRINT '======================================================'

-- =============================================
-- 1. FLRTTenantCredentials
--    API credentials for OData access (one row per company).
-- =============================================
PRINT ''
PRINT '--- 1. FLRTTenantCredentials ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTTenantCredentials...'

    CREATE TABLE [dbo].[FLRTTenantCredentials] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [CompanyNum]             [int]              NOT NULL,
        [TenantName]             [nvarchar](50)     NULL,
        [BaseURL]                [nvarchar](255)    NULL,
        [UsernameNew]            [nvarchar](255)    NULL,
        [PasswordNew]            [nvarchar](255)    NULL,
        [ClientIDNew]            [nvarchar](255)    NULL,
        [ClientSecretNew]        [nvarchar](255)    NULL,
        [GammaApiKey]            [nvarchar](500)    NULL,
        [NoteID]                 [uniqueidentifier] NOT NULL DEFAULT(NEWID()),
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTTenantCredentials] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [CompanyNum] ASC)
    )

    PRINT 'FLRTTenantCredentials created.'
END
ELSE
    PRINT 'FLRTTenantCredentials already exists. Skipping.'
GO

-- =============================================
-- 2. FLRTReportDefinition
--    Defines a GL report structure: GI mapping, column config, rounding.
--    One definition = one financial statement (BS, PL, CF, etc.)
-- =============================================
PRINT ''
PRINT '--- 2. FLRTReportDefinition ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTReportDefinition...'

    CREATE TABLE [dbo].[FLRTReportDefinition] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [DefinitionID]           [int]              IDENTITY(1,1) NOT NULL,
        [DefinitionCD]           [nvarchar](50)     NULL,
        [DefinitionPrefix]       [nvarchar](10)     NULL,
        [Description]            [nvarchar](255)    NULL,
        [ReportType]             [nvarchar](10)     NOT NULL DEFAULT('BS'),
        [IsActive]               [bit]              NOT NULL DEFAULT(1),

        -- GI / Column Mapping
        [GIName]                 [nvarchar](100)    NOT NULL DEFAULT('TrialBalance'),
        [AccountColumn]          [nvarchar](100)    NOT NULL DEFAULT('Account'),
        [TypeColumn]             [nvarchar](100)    NOT NULL DEFAULT('Type'),
        [BeginningBalColumn]     [nvarchar](100)    NOT NULL DEFAULT('BeginningBalance'),
        [EndingBalColumn]        [nvarchar](100)    NOT NULL DEFAULT('EndingBalance'),
        [DebitColumn]            [nvarchar](100)    NOT NULL DEFAULT('Debit'),
        [CreditColumn]           [nvarchar](100)    NOT NULL DEFAULT('Credit'),

        -- Formatting
        [RoundingLevel]          [nvarchar](10)     NOT NULL DEFAULT('UNITS'),
        [DecimalPlaces]          [int]              NOT NULL DEFAULT(0),

        -- Audit
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTReportDefinition] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [DefinitionID] ASC)
    )

    PRINT 'FLRTReportDefinition created.'
END
ELSE
    PRINT 'FLRTReportDefinition already exists. Skipping.'
GO

-- =============================================
-- 3. FLRTReportLineItem
--    Line items belonging to a definition.
--    Each row maps an account range (or formula) to a placeholder code.
-- =============================================
PRINT ''
PRINT '--- 3. FLRTReportLineItem ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTReportLineItem...'

    CREATE TABLE [dbo].[FLRTReportLineItem] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [LineID]                 [int]              IDENTITY(1,1) NOT NULL,
        [DefinitionID]           [int]              NULL,
        [SortOrder]              [int]              NOT NULL DEFAULT(0),
        [LineCode]               [nvarchar](100)    NULL,
        [Description]            [nvarchar](255)    NULL,
        [LineType]               [nvarchar](20)     NOT NULL DEFAULT('ACCOUNT'),
        [AccountFrom]            [nvarchar](50)     NULL,
        [AccountTo]              [nvarchar](50)     NULL,
        [AccountTypeFilter]      [nvarchar](5)      NULL,
        [SignRule]               [nvarchar](10)     NOT NULL DEFAULT('ASIS'),
        [BalanceType]            [nvarchar](15)     NOT NULL DEFAULT('ENDING'),
        [ParentLineCode]         [nvarchar](100)    NULL,
        [Formula]                [nvarchar](500)    NULL,
        [IsVisible]              [bit]              NOT NULL DEFAULT(1),
        [SubaccountFilter]       [nvarchar](30)     NULL,
        [BranchFilter]           [nvarchar](30)     NULL,
        [OrganizationFilter]     [nvarchar](30)     NULL,
        [LedgerFilter]           [nvarchar](20)     NULL,
        [NoteID]                 [uniqueidentifier] NOT NULL DEFAULT(NEWID()),

        -- Audit
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTReportLineItem] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [LineID] ASC)
    )

    CREATE NONCLUSTERED INDEX [IX_FLRTReportLineItem_DefinitionID]
        ON [dbo].[FLRTReportLineItem] ([CompanyID] ASC, [DefinitionID] ASC, [SortOrder] ASC)

    PRINT 'FLRTReportLineItem created.'
END
ELSE
    PRINT 'FLRTReportLineItem already exists. Skipping.'
GO

-- =============================================
-- 4. FLRTFinancialReport
--    Master report record: year, period, org, branch, ledger, template file.
-- =============================================
PRINT ''
PRINT '--- 4. FLRTFinancialReport ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTFinancialReport...'

    CREATE TABLE [dbo].[FLRTFinancialReport] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [ReportID]               [int]              IDENTITY(1,1) NOT NULL,
        [ReportCD]               [nvarchar](225)    NULL,
        [Description]            [nvarchar](50)     NULL,
        [CurrYear]               [nvarchar](4)      NULL,
        [FinancialMonth]         [nvarchar](2)      NOT NULL DEFAULT('12'),
        [Branch]                 [nvarchar](10)     NULL,
        [Organization]           [nvarchar](50)     NULL,
        [Ledger]                 [nvarchar](20)     NULL,
        [DefinitionID]           [int]              NULL,   -- legacy single-def link
        [GeneratedFileID]        [uniqueidentifier] NULL,
        [UploadedFileID]         [uniqueidentifier] NULL,
        [UploadedFileIDDisplay]  [nvarchar](225)    NULL,
        [Status]                 [nvarchar](1)      NOT NULL DEFAULT('N'),
        [CompanyNum]             [int]              NULL,
        [NoteID]                 [uniqueidentifier] NOT NULL DEFAULT(NEWID()),

        -- Audit
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTFinancialReport] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [ReportID] ASC)
    )

    PRINT 'FLRTFinancialReport created.'
END
ELSE
    PRINT 'FLRTFinancialReport already exists. Skipping.'
GO

-- =============================================
-- 5. FLRTReportDefinitionLink
--    Links one or more definitions to a Financial Report (multi-definition support).
--    Supersedes the legacy FLRTFinancialReport.DefinitionID field.
-- =============================================
PRINT ''
PRINT '--- 5. FLRTReportDefinitionLink ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinitionLink]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTReportDefinitionLink...'

    CREATE TABLE [dbo].[FLRTReportDefinitionLink] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [LinkID]                 [int]              IDENTITY(1,1) NOT NULL,
        [ReportID]               [int]              NULL,
        [DefinitionID]           [int]              NULL,
        [DisplayOrder]           [int]              NOT NULL DEFAULT(0),

        -- Audit
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTReportDefinitionLink] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [LinkID] ASC)
    )

    CREATE NONCLUSTERED INDEX [IX_FLRTReportDefinitionLink_ReportID]
        ON [dbo].[FLRTReportDefinitionLink] ([CompanyID] ASC, [ReportID] ASC, [DisplayOrder] ASC)

    PRINT 'FLRTReportDefinitionLink created.'
END
ELSE
    PRINT 'FLRTReportDefinitionLink already exists. Skipping.'
GO

-- =============================================
-- 6. FLRTPresentationGeneration
--    Standalone presentation record (decoupled from Financial Report).
--    Holds Gamma template reference, status, generated file, and markdown.
-- =============================================
PRINT ''
PRINT '--- 6. FLRTPresentationGeneration ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTPresentationGeneration...'

    CREATE TABLE [dbo].[FLRTPresentationGeneration] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [PresentationID]         [int]              IDENTITY(1,1) NOT NULL,
        [PresentationCD]         [nvarchar](225)    NULL,
        [Description]            [nvarchar](50)     NULL,
        [CurrYear]               [nvarchar](4)      NULL,
        [FinancialMonth]         [nvarchar](2)      NOT NULL DEFAULT('12'),
        [Branch]                 [nvarchar](10)     NULL,
        [Organization]           [nvarchar](50)     NULL,
        [Ledger]                 [nvarchar](20)     NULL,
        [PresentationTitle]      [nvarchar](500)    NULL,
        [PresentationDescription][nvarchar](2000)   NULL,
        [GammaTemplateId]        [nvarchar](100)    NULL,
        [SlideGeneratedFileID]   [uniqueidentifier] NULL,
        [PresentationMarkdown]   [nvarchar](max)    NULL,
        [SlideStatus]            [nvarchar](1)      NOT NULL DEFAULT('N'),
        [CompanyNum]             [int]              NULL,
        [NoteID]                 [uniqueidentifier] NOT NULL DEFAULT(NEWID()),

        -- Audit
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTPresentationGeneration] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [PresentationID] ASC)
    )

    PRINT 'FLRTPresentationGeneration created.'
END
ELSE
    PRINT 'FLRTPresentationGeneration already exists. Skipping.'
GO

-- =============================================
-- 7. FLRTPresentationDefinitionLink
--    Links GL Report Definitions to a Presentation (one-to-many).
-- =============================================
PRINT ''
PRINT '--- 7. FLRTPresentationDefinitionLink ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationDefinitionLink]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTPresentationDefinitionLink...'

    CREATE TABLE [dbo].[FLRTPresentationDefinitionLink] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [LinkID]                 [int]              IDENTITY(1,1) NOT NULL,
        [PresentationID]         [int]              NULL,
        [DefinitionID]           [int]              NULL,
        [DisplayOrder]           [int]              NOT NULL DEFAULT(0),

        -- Audit
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTPresentationDefinitionLink] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [LinkID] ASC)
    )

    CREATE NONCLUSTERED INDEX [IX_FLRTPresentationDefinitionLink_PresentationID]
        ON [dbo].[FLRTPresentationDefinitionLink] ([CompanyID] ASC, [PresentationID] ASC, [DisplayOrder] ASC)

    PRINT 'FLRTPresentationDefinitionLink created.'
END
ELSE
    PRINT 'FLRTPresentationDefinitionLink already exists. Skipping.'
GO

-- =============================================
-- 8. FLRTGIDataSource
--    Generic GI data source: maps any GI to placeholder values for presentations.
--    Not GL-specific — supports any column type with configurable filter columns.
-- =============================================
PRINT ''
PRINT '--- 8. FLRTGIDataSource ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTGIDataSource...'

    CREATE TABLE [dbo].[FLRTGIDataSource] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [DataSourceID]           [int]              IDENTITY(1,1) NOT NULL,
        [DataSourceCD]           [nvarchar](50)     NULL,
        [Description]            [nvarchar](255)    NULL,
        [Prefix]                 [nvarchar](10)     NULL,
        [IsActive]               [bit]              NOT NULL DEFAULT(1),

        -- GI Configuration
        [GIName]                 [nvarchar](100)    NULL,
        [KeyColumn]              [nvarchar](100)    NULL,

        -- Period filter
        [PeriodFilterColumn]     [nvarchar](100)    NULL,
        [PeriodFilterType]       [nvarchar](10)     NOT NULL DEFAULT('String'),
        [PeriodFilterTemplate]   [nvarchar](50)     NULL,

        -- Branch filter
        [BranchFilterColumn]     [nvarchar](100)    NULL,
        [BranchFilterType]       [nvarchar](10)     NOT NULL DEFAULT('String'),

        -- Organization filter
        [OrgFilterColumn]        [nvarchar](100)    NULL,
        [OrgFilterType]          [nvarchar](10)     NOT NULL DEFAULT('String'),

        -- Ledger filter
        [LedgerFilterColumn]     [nvarchar](100)    NULL,
        [LedgerFilterType]       [nvarchar](10)     NOT NULL DEFAULT('String'),

        -- Audit
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTGIDataSource] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [DataSourceID] ASC)
    )

    PRINT 'FLRTGIDataSource created.'
END
ELSE
    PRINT 'FLRTGIDataSource already exists. Skipping.'
GO

-- =============================================
-- 9. FLRTGIDataSourceColumn
--    Column definitions for a GI Data Source.
--    Each row = one placeholder: {{PREFIX_ALIAS}}.
--    Supports type-aware aggregation and arithmetic formulas.
-- =============================================
PRINT ''
PRINT '--- 9. FLRTGIDataSourceColumn ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTGIDataSourceColumn...'

    CREATE TABLE [dbo].[FLRTGIDataSourceColumn] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [ColumnID]               [int]              IDENTITY(1,1) NOT NULL,
        [DataSourceID]           [int]              NULL,
        [SortOrder]              [int]              NOT NULL DEFAULT(0),

        -- Identity
        [ColumnAlias]            [nvarchar](100)    NULL,
        [Description]            [nvarchar](255)    NULL,
        [LineType]               [nvarchar](20)     NOT NULL DEFAULT('VALUE'),

        -- Value fields
        [GIColumn]               [nvarchar](100)    NULL,
        [ColumnType]             [nvarchar](10)     NOT NULL DEFAULT('Decimal'),
        [AggregateFunction]      [nvarchar](10)     NOT NULL DEFAULT('SUM'),
        [KeyFrom]                [nvarchar](100)    NULL,
        [KeyTo]                  [nvarchar](100)    NULL,
        [RowFilter]              [nvarchar](500)    NULL,

        -- Calculated fields
        [Formula]                [nvarchar](500)    NULL,

        -- Display
        [FormatString]           [nvarchar](50)     NULL,
        [IsVisible]              [bit]              NOT NULL DEFAULT(1),

        -- Audit
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTGIDataSourceColumn] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [ColumnID] ASC)
    )

    CREATE NONCLUSTERED INDEX [IX_FLRTGIDataSourceColumn_DataSourceID]
        ON [dbo].[FLRTGIDataSourceColumn] ([CompanyID] ASC, [DataSourceID] ASC, [SortOrder] ASC)

    PRINT 'FLRTGIDataSourceColumn created.'
END
ELSE
    PRINT 'FLRTGIDataSourceColumn already exists. Skipping.'
GO

PRINT ''
PRINT '======================================================'
PRINT 'Full schema creation complete.'
PRINT ''
PRINT 'Tables created/verified:'
PRINT '  1. FLRTTenantCredentials'
PRINT '  2. FLRTReportDefinition'
PRINT '  3. FLRTReportLineItem'
PRINT '  4. FLRTFinancialReport'
PRINT '  5. FLRTReportDefinitionLink'
PRINT '  6. FLRTPresentationGeneration'
PRINT '  7. FLRTPresentationDefinitionLink'
PRINT '  8. FLRTGIDataSource'
PRINT '  9. FLRTGIDataSourceColumn'
PRINT '======================================================'
