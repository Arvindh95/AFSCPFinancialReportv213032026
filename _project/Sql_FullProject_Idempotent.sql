-- =============================================
-- Full Project SQL: AFSCPFinancialReport (Idempotent)
-- Safe to run on ANY database state:
--   - Fresh database   → CREATE TABLE with correct schema
--   - Old deployment   → ADD missing columns, MIGRATE legacy schema
--   - Current database → No-op (all checks pass harmlessly)
-- =============================================

PRINT '======================================================'
PRINT 'AFSCPFinancialReport - Idempotent Schema Sync'
PRINT '======================================================'

-- =============================================
-- 1. FLRTTenantCredentials
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
BEGIN
    PRINT 'FLRTTenantCredentials exists. Checking columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = N'TenantName')
        ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [TenantName] [nvarchar](50) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = N'BaseURL')
        ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [BaseURL] [nvarchar](255) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = N'UsernameNew')
        ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [UsernameNew] [nvarchar](255) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = N'PasswordNew')
        ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [PasswordNew] [nvarchar](255) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = N'ClientIDNew')
        ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [ClientIDNew] [nvarchar](255) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = N'ClientSecretNew')
        ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [ClientSecretNew] [nvarchar](255) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = N'GammaApiKey')
        ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [GammaApiKey] [nvarchar](500) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = N'NoteID')
        ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [NoteID] [uniqueidentifier] NOT NULL DEFAULT(NEWID());

    -- Migration: drop orphaned columns from old schema
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = N'AlaiApiKey')
    BEGIN
        ALTER TABLE [dbo].[FLRTTenantCredentials] DROP COLUMN [AlaiApiKey];
        PRINT '  ~ Dropped orphaned column AlaiApiKey';
    END

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = N'SlidesGptApiKey')
    BEGIN
        ALTER TABLE [dbo].[FLRTTenantCredentials] DROP COLUMN [SlidesGptApiKey];
        PRINT '  ~ Dropped orphaned column SlidesGptApiKey';
    END

    -- Migration: widen GammaApiKey from nvarchar(255) to nvarchar(500) if needed
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'FLRTTenantCredentials' AND COLUMN_NAME = 'GammaApiKey'
                 AND CHARACTER_MAXIMUM_LENGTH < 500)
    BEGIN
        ALTER TABLE [dbo].[FLRTTenantCredentials] ALTER COLUMN [GammaApiKey] [nvarchar](500) NULL;
        PRINT '  ~ GammaApiKey widened to nvarchar(500)';
    END

    PRINT 'FLRTTenantCredentials column check complete.'
END
GO

-- =============================================
-- 2. FLRTReportDefinition
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
        [GIName]                 [nvarchar](100)    NOT NULL DEFAULT('TrialBalance'),
        [AccountColumn]          [nvarchar](100)    NOT NULL DEFAULT('Account'),
        [TypeColumn]             [nvarchar](100)    NOT NULL DEFAULT('Type'),
        [BeginningBalColumn]     [nvarchar](100)    NOT NULL DEFAULT('BeginningBalance'),
        [EndingBalColumn]        [nvarchar](100)    NOT NULL DEFAULT('EndingBalance'),
        [DebitColumn]            [nvarchar](100)    NOT NULL DEFAULT('Debit'),
        [CreditColumn]           [nvarchar](100)    NOT NULL DEFAULT('Credit'),
        [RoundingLevel]          [nvarchar](10)     NOT NULL DEFAULT('UNITS'),
        [DecimalPlaces]          [int]              NOT NULL DEFAULT(0),
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
BEGIN
    PRINT 'FLRTReportDefinition exists. Checking columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'DefinitionCD')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [DefinitionCD] [nvarchar](50) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'DefinitionPrefix')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [DefinitionPrefix] [nvarchar](10) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'Description')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [Description] [nvarchar](255) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'ReportType')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [ReportType] [nvarchar](10) NOT NULL DEFAULT('BS');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'IsActive')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [IsActive] [bit] NOT NULL DEFAULT(1);

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'GIName')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [GIName] [nvarchar](100) NOT NULL DEFAULT('TrialBalance');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'AccountColumn')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [AccountColumn] [nvarchar](100) NOT NULL DEFAULT('Account');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'TypeColumn')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [TypeColumn] [nvarchar](100) NOT NULL DEFAULT('Type');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'BeginningBalColumn')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [BeginningBalColumn] [nvarchar](100) NOT NULL DEFAULT('BeginningBalance');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'EndingBalColumn')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [EndingBalColumn] [nvarchar](100) NOT NULL DEFAULT('EndingBalance');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'DebitColumn')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [DebitColumn] [nvarchar](100) NOT NULL DEFAULT('Debit');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'CreditColumn')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [CreditColumn] [nvarchar](100) NOT NULL DEFAULT('Credit');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'RoundingLevel')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [RoundingLevel] [nvarchar](10) NOT NULL DEFAULT('UNITS');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = N'DecimalPlaces')
        ALTER TABLE [dbo].[FLRTReportDefinition] ADD [DecimalPlaces] [int] NOT NULL DEFAULT(0);

    -- Migration: rebuild PK from (CompanyID, DefinitionCD) to (CompanyID, DefinitionID)
    -- Old script used DefinitionCD in the PK; current schema uses the IDENTITY DefinitionID.
    IF EXISTS (
        SELECT 1 FROM sys.index_columns ic
        INNER JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE i.object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]')
          AND i.is_primary_key = 1
          AND c.name = N'DefinitionCD'
    )
    BEGIN
        PRINT '  ~ Rebuilding PK: (CompanyID, DefinitionCD) -> (CompanyID, DefinitionID)';

        DECLARE @rdPK NVARCHAR(128);
        SELECT @rdPK = name FROM sys.key_constraints
            WHERE parent_object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND type = 'PK';
        EXEC('ALTER TABLE [dbo].[FLRTReportDefinition] DROP CONSTRAINT [' + @rdPK + ']');

        ALTER TABLE [dbo].[FLRTReportDefinition]
            ADD CONSTRAINT [PK_FLRTReportDefinition] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [DefinitionID] ASC);

        PRINT '  ~ PK rebuilt on (CompanyID, DefinitionID)';
    END

    PRINT 'FLRTReportDefinition column check complete.'
END
GO

-- =============================================
-- 3. FLRTReportLineItem
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
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTReportLineItem] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [LineID] ASC)
    )

    PRINT 'FLRTReportLineItem created.'
END
ELSE
BEGIN
    PRINT 'FLRTReportLineItem exists. Checking columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'DefinitionID')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [DefinitionID] [int] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'SortOrder')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [SortOrder] [int] NOT NULL DEFAULT(0);

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'LineCode')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [LineCode] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'Description')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [Description] [nvarchar](255) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'LineType')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [LineType] [nvarchar](20) NOT NULL DEFAULT('ACCOUNT');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'AccountFrom')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [AccountFrom] [nvarchar](50) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'AccountTo')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [AccountTo] [nvarchar](50) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'AccountTypeFilter')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [AccountTypeFilter] [nvarchar](5) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'SignRule')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [SignRule] [nvarchar](10) NOT NULL DEFAULT('ASIS');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'BalanceType')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [BalanceType] [nvarchar](15) NOT NULL DEFAULT('ENDING');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'ParentLineCode')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [ParentLineCode] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'Formula')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [Formula] [nvarchar](500) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'IsVisible')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [IsVisible] [bit] NOT NULL DEFAULT(1);

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'SubaccountFilter')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [SubaccountFilter] [nvarchar](30) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'BranchFilter')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [BranchFilter] [nvarchar](30) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'OrganizationFilter')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [OrganizationFilter] [nvarchar](30) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'LedgerFilter')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [LedgerFilter] [nvarchar](20) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'NoteID')
        ALTER TABLE [dbo].[FLRTReportLineItem] ADD [NoteID] [uniqueidentifier] NOT NULL DEFAULT(NEWID());

    PRINT 'FLRTReportLineItem column check complete.'
END
GO

-- Ensure index exists
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = N'IX_FLRTReportLineItem_DefinitionID')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FLRTReportLineItem_DefinitionID]
        ON [dbo].[FLRTReportLineItem] ([CompanyID] ASC, [DefinitionID] ASC, [SortOrder] ASC);
    PRINT 'Index IX_FLRTReportLineItem_DefinitionID created.'
END
GO

-- =============================================
-- 4. FLRTFinancialReport
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
        [DefinitionID]           [int]              NULL,
        [GeneratedFileID]        [uniqueidentifier] NULL,
        [UploadedFileID]         [uniqueidentifier] NULL,
        [UploadedFileIDDisplay]  [nvarchar](225)    NULL,
        [Status]                 [nvarchar](1)      NOT NULL DEFAULT('N'),
        [CompanyNum]             [int]              NULL,
        [NoteID]                 [uniqueidentifier] NOT NULL DEFAULT(NEWID()),
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
BEGIN
    PRINT 'FLRTFinancialReport exists. Checking columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'ReportCD')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [ReportCD] [nvarchar](225) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'Description')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [Description] [nvarchar](50) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'CurrYear')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [CurrYear] [nvarchar](4) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'FinancialMonth')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [FinancialMonth] [nvarchar](2) NOT NULL DEFAULT('12');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'Branch')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [Branch] [nvarchar](10) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'Organization')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [Organization] [nvarchar](50) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'Ledger')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [Ledger] [nvarchar](20) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'DefinitionID')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [DefinitionID] [int] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'GeneratedFileID')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [GeneratedFileID] [uniqueidentifier] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'UploadedFileID')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [UploadedFileID] [uniqueidentifier] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'UploadedFileIDDisplay')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [UploadedFileIDDisplay] [nvarchar](225) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'Status')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [Status] [nvarchar](1) NOT NULL DEFAULT('N');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'CompanyNum')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [CompanyNum] [int] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'NoteID')
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD [NoteID] [uniqueidentifier] NOT NULL DEFAULT(NEWID());

    -- Migration: convert Status from old full-text values to 1-char codes, then shrink column
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'FLRTFinancialReport' AND COLUMN_NAME = 'Status'
                 AND CHARACTER_MAXIMUM_LENGTH > 1)
    BEGIN
        PRINT '  ~ Migrating Status from nvarchar(100) to nvarchar(1)';

        -- Map old text values to new 1-char codes
        UPDATE [dbo].[FLRTFinancialReport] SET [Status] = 'N' WHERE [Status] IN ('File not Generated', 'Not Generated', 'Pending');
        UPDATE [dbo].[FLRTFinancialReport] SET [Status] = 'P' WHERE [Status] = 'In Progress';
        UPDATE [dbo].[FLRTFinancialReport] SET [Status] = 'C' WHERE [Status] IN ('Ready to Download', 'Completed');
        UPDATE [dbo].[FLRTFinancialReport] SET [Status] = 'F' WHERE [Status] = 'Failed';
        -- Catch-all: any unrecognised value defaults to N
        UPDATE [dbo].[FLRTFinancialReport] SET [Status] = 'N' WHERE LEN([Status]) > 1;

        -- Drop the old default constraint before altering the column
        DECLARE @frStatusDC NVARCHAR(256);
        SELECT @frStatusDC = dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE c.object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND c.name = 'Status';
        IF @frStatusDC IS NOT NULL
            EXEC('ALTER TABLE [dbo].[FLRTFinancialReport] DROP CONSTRAINT [' + @frStatusDC + ']');

        ALTER TABLE [dbo].[FLRTFinancialReport] ALTER COLUMN [Status] [nvarchar](1) NOT NULL;
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD DEFAULT('N') FOR [Status];

        PRINT '  ~ Status migrated to nvarchar(1) with code values (N/P/C/F)';
    END

    -- Migration: shrink FinancialMonth from nvarchar(50) to nvarchar(2)
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'FLRTFinancialReport' AND COLUMN_NAME = 'FinancialMonth'
                 AND CHARACTER_MAXIMUM_LENGTH > 2)
    BEGIN
        PRINT '  ~ Shrinking FinancialMonth to nvarchar(2)';

        DECLARE @frMonthDC NVARCHAR(256);
        SELECT @frMonthDC = dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE c.object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND c.name = 'FinancialMonth';
        IF @frMonthDC IS NOT NULL
            EXEC('ALTER TABLE [dbo].[FLRTFinancialReport] DROP CONSTRAINT [' + @frMonthDC + ']');

        -- Truncate any values longer than 2 chars (shouldn't exist, but safety)
        UPDATE [dbo].[FLRTFinancialReport] SET [FinancialMonth] = LEFT([FinancialMonth], 2)
            WHERE LEN([FinancialMonth]) > 2;

        ALTER TABLE [dbo].[FLRTFinancialReport] ALTER COLUMN [FinancialMonth] [nvarchar](2) NOT NULL;
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD DEFAULT('12') FOR [FinancialMonth];

        PRINT '  ~ FinancialMonth shrunk to nvarchar(2)';
    END

    -- Migration: drop orphaned Selected column
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = N'Selected')
    BEGIN
        DECLARE @frSelDC NVARCHAR(256);
        SELECT @frSelDC = dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE c.object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND c.name = 'Selected';
        IF @frSelDC IS NOT NULL
            EXEC('ALTER TABLE [dbo].[FLRTFinancialReport] DROP CONSTRAINT [' + @frSelDC + ']');

        ALTER TABLE [dbo].[FLRTFinancialReport] DROP COLUMN [Selected];
        PRINT '  ~ Dropped orphaned column Selected';
    END

    PRINT 'FLRTFinancialReport column check complete.'
END
GO

-- =============================================
-- 5. FLRTReportDefinitionLink
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
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTReportDefinitionLink] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [LinkID] ASC)
    )

    PRINT 'FLRTReportDefinitionLink created.'
END
ELSE
BEGIN
    PRINT 'FLRTReportDefinitionLink exists. Checking columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinitionLink]') AND name = N'ReportID')
        ALTER TABLE [dbo].[FLRTReportDefinitionLink] ADD [ReportID] [int] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinitionLink]') AND name = N'DefinitionID')
        ALTER TABLE [dbo].[FLRTReportDefinitionLink] ADD [DefinitionID] [int] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinitionLink]') AND name = N'DisplayOrder')
        ALTER TABLE [dbo].[FLRTReportDefinitionLink] ADD [DisplayOrder] [int] NOT NULL DEFAULT(0);

    PRINT 'FLRTReportDefinitionLink column check complete.'
END
GO

-- Ensure index exists
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinitionLink]') AND name = N'IX_FLRTReportDefinitionLink_ReportID')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FLRTReportDefinitionLink_ReportID]
        ON [dbo].[FLRTReportDefinitionLink] ([CompanyID] ASC, [ReportID] ASC, [DisplayOrder] ASC);
    PRINT 'Index IX_FLRTReportDefinitionLink_ReportID created.'
END
GO

-- =============================================
-- 6. FLRTPresentationGeneration
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
BEGIN
    PRINT 'FLRTPresentationGeneration exists. Checking columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'PresentationCD')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [PresentationCD] [nvarchar](225) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'Description')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [Description] [nvarchar](50) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'CurrYear')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [CurrYear] [nvarchar](4) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'FinancialMonth')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [FinancialMonth] [nvarchar](2) NOT NULL DEFAULT('12');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'Branch')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [Branch] [nvarchar](10) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'Organization')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [Organization] [nvarchar](50) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'Ledger')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [Ledger] [nvarchar](20) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'PresentationTitle')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [PresentationTitle] [nvarchar](500) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'PresentationDescription')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [PresentationDescription] [nvarchar](2000) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'GammaTemplateId')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [GammaTemplateId] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'SlideGeneratedFileID')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [SlideGeneratedFileID] [uniqueidentifier] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'PresentationMarkdown')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [PresentationMarkdown] [nvarchar](max) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'SlideStatus')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [SlideStatus] [nvarchar](1) NOT NULL DEFAULT('N');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'CompanyNum')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [CompanyNum] [int] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND name = N'NoteID')
        ALTER TABLE [dbo].[FLRTPresentationGeneration] ADD [NoteID] [uniqueidentifier] NOT NULL DEFAULT(NEWID());

    PRINT 'FLRTPresentationGeneration column check complete.'
END
GO

-- =============================================
-- 7. FLRTPresentationDefinitionLink
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
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTPresentationDefinitionLink] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [LinkID] ASC)
    )

    PRINT 'FLRTPresentationDefinitionLink created.'
END
ELSE
BEGIN
    PRINT 'FLRTPresentationDefinitionLink exists. Checking columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationDefinitionLink]') AND name = N'PresentationID')
        ALTER TABLE [dbo].[FLRTPresentationDefinitionLink] ADD [PresentationID] [int] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationDefinitionLink]') AND name = N'DefinitionID')
        ALTER TABLE [dbo].[FLRTPresentationDefinitionLink] ADD [DefinitionID] [int] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationDefinitionLink]') AND name = N'DisplayOrder')
        ALTER TABLE [dbo].[FLRTPresentationDefinitionLink] ADD [DisplayOrder] [int] NOT NULL DEFAULT(0);

    PRINT 'FLRTPresentationDefinitionLink column check complete.'
END
GO

-- Ensure index exists
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationDefinitionLink]') AND name = N'IX_FLRTPresentationDefinitionLink_PresentationID')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FLRTPresentationDefinitionLink_PresentationID]
        ON [dbo].[FLRTPresentationDefinitionLink] ([CompanyID] ASC, [PresentationID] ASC, [DisplayOrder] ASC);
    PRINT 'Index IX_FLRTPresentationDefinitionLink_PresentationID created.'
END
GO

-- =============================================
-- 8. FLRTGIDataSource
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
        [GIName]                 [nvarchar](100)    NULL,
        [KeyColumn]              [nvarchar](100)    NULL,
        [PeriodFilterColumn]     [nvarchar](100)    NULL,
        [PeriodFilterType]       [nvarchar](10)     NOT NULL DEFAULT('String'),
        [PeriodFilterTemplate]   [nvarchar](50)     NULL,
        [BranchFilterColumn]     [nvarchar](100)    NULL,
        [BranchFilterType]       [nvarchar](10)     NOT NULL DEFAULT('String'),
        [OrgFilterColumn]        [nvarchar](100)    NULL,
        [OrgFilterType]          [nvarchar](10)     NOT NULL DEFAULT('String'),
        [LedgerFilterColumn]     [nvarchar](100)    NULL,
        [LedgerFilterType]       [nvarchar](10)     NOT NULL DEFAULT('String'),
        [PeriodScope]            [nvarchar](10)     NOT NULL DEFAULT('Monthly'),
        [DetectedColumns]        [nvarchar](4000)   NULL,
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
BEGIN
    PRINT 'FLRTGIDataSource exists. Checking columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'DataSourceCD')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [DataSourceCD] [nvarchar](50) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'Description')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [Description] [nvarchar](255) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'Prefix')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [Prefix] [nvarchar](10) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'IsActive')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [IsActive] [bit] NOT NULL DEFAULT(1);

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'GIName')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [GIName] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'KeyColumn')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [KeyColumn] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'PeriodFilterColumn')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [PeriodFilterColumn] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'PeriodFilterType')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [PeriodFilterType] [nvarchar](10) NOT NULL DEFAULT('String');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'PeriodFilterTemplate')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [PeriodFilterTemplate] [nvarchar](50) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'BranchFilterColumn')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [BranchFilterColumn] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'BranchFilterType')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [BranchFilterType] [nvarchar](10) NOT NULL DEFAULT('String');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'OrgFilterColumn')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [OrgFilterColumn] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'OrgFilterType')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [OrgFilterType] [nvarchar](10) NOT NULL DEFAULT('String');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'LedgerFilterColumn')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [LedgerFilterColumn] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'LedgerFilterType')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [LedgerFilterType] [nvarchar](10) NOT NULL DEFAULT('String');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'PeriodScope')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [PeriodScope] [nvarchar](10) NOT NULL DEFAULT('Monthly');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSource]') AND name = N'DetectedColumns')
        ALTER TABLE [dbo].[FLRTGIDataSource] ADD [DetectedColumns] [nvarchar](4000) NULL;

    PRINT 'FLRTGIDataSource column check complete.'
END
GO

-- =============================================
-- 9. FLRTGIDataSourceColumn
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
        [ColumnAlias]            [nvarchar](100)    NULL,
        [Description]            [nvarchar](255)    NULL,
        [LineType]               [nvarchar](20)     NOT NULL DEFAULT('VALUE'),
        [GIColumn]               [nvarchar](100)    NULL,
        [ColumnType]             [nvarchar](10)     NOT NULL DEFAULT('Decimal'),
        [AggregateFunction]      [nvarchar](10)     NOT NULL DEFAULT('SUM'),
        [KeyFrom]                [nvarchar](100)    NULL,
        [KeyTo]                  [nvarchar](100)    NULL,
        [RowFilter]              [nvarchar](500)    NULL,
        [Formula]                [nvarchar](500)    NULL,
        [FormatString]           [nvarchar](50)     NULL,
        [IsVisible]              [bit]              NOT NULL DEFAULT(1),
        [OrderByColumn]          [nvarchar](100)    NULL,
        [OrderByDirection]       [nvarchar](4)      NOT NULL DEFAULT('DESC'),
        [RowLimit]               [int]              NOT NULL DEFAULT(10),
        [DisplayColumns]         [nvarchar](500)    NULL,
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTGIDataSourceColumn] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [ColumnID] ASC)
    )

    PRINT 'FLRTGIDataSourceColumn created.'
END
ELSE
BEGIN
    PRINT 'FLRTGIDataSourceColumn exists. Checking columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'DataSourceID')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [DataSourceID] [int] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'SortOrder')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [SortOrder] [int] NOT NULL DEFAULT(0);

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'ColumnAlias')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [ColumnAlias] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'Description')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [Description] [nvarchar](255) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'LineType')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [LineType] [nvarchar](20) NOT NULL DEFAULT('VALUE');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'GIColumn')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [GIColumn] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'ColumnType')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [ColumnType] [nvarchar](10) NOT NULL DEFAULT('Decimal');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'AggregateFunction')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [AggregateFunction] [nvarchar](10) NOT NULL DEFAULT('SUM');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'KeyFrom')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [KeyFrom] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'KeyTo')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [KeyTo] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'RowFilter')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [RowFilter] [nvarchar](500) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'Formula')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [Formula] [nvarchar](500) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'FormatString')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [FormatString] [nvarchar](50) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'IsVisible')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [IsVisible] [bit] NOT NULL DEFAULT(1);

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'OrderByColumn')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [OrderByColumn] [nvarchar](100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'OrderByDirection')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [OrderByDirection] [nvarchar](4) NOT NULL DEFAULT('DESC');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'RowLimit')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [RowLimit] [int] NOT NULL DEFAULT(10);

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'DisplayColumns')
        ALTER TABLE [dbo].[FLRTGIDataSourceColumn] ADD [DisplayColumns] [nvarchar](500) NULL;

    PRINT 'FLRTGIDataSourceColumn column check complete.'
END
GO

-- Ensure index exists
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[FLRTGIDataSourceColumn]') AND name = N'IX_FLRTGIDataSourceColumn_DataSourceID')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FLRTGIDataSourceColumn_DataSourceID]
        ON [dbo].[FLRTGIDataSourceColumn] ([CompanyID] ASC, [DataSourceID] ASC, [SortOrder] ASC);
    PRINT 'Index IX_FLRTGIDataSourceColumn_DataSourceID created.'
END
GO

-- =============================================
-- 10. FLRTPresentationDataSourceLink
-- =============================================
PRINT ''
PRINT '--- 10. FLRTPresentationDataSourceLink ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationDataSourceLink]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTPresentationDataSourceLink...'

    CREATE TABLE [dbo].[FLRTPresentationDataSourceLink] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [LinkID]                 [int]              IDENTITY(1,1) NOT NULL,
        [PresentationID]         [int]              NULL,
        [DataSourceID]           [int]              NULL,
        [DisplayOrder]           [int]              NOT NULL DEFAULT(0),
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTPresentationDataSourceLink] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [LinkID] ASC)
    )

    PRINT 'FLRTPresentationDataSourceLink created.'
END
ELSE
BEGIN
    PRINT 'FLRTPresentationDataSourceLink exists. Checking columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationDataSourceLink]') AND name = N'PresentationID')
        ALTER TABLE [dbo].[FLRTPresentationDataSourceLink] ADD [PresentationID] [int] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationDataSourceLink]') AND name = N'DataSourceID')
        ALTER TABLE [dbo].[FLRTPresentationDataSourceLink] ADD [DataSourceID] [int] NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationDataSourceLink]') AND name = N'DisplayOrder')
        ALTER TABLE [dbo].[FLRTPresentationDataSourceLink] ADD [DisplayOrder] [int] NOT NULL DEFAULT(0);

    PRINT 'FLRTPresentationDataSourceLink column check complete.'
END
GO

-- Ensure index exists
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationDataSourceLink]') AND name = N'IX_FLRTPresentationDataSourceLink_PresentationID')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FLRTPresentationDataSourceLink_PresentationID]
        ON [dbo].[FLRTPresentationDataSourceLink] ([CompanyID] ASC, [PresentationID] ASC, [DisplayOrder] ASC);
    PRINT 'Index IX_FLRTPresentationDataSourceLink_PresentationID created.'
END
GO

-- =============================================
-- Summary
-- =============================================
PRINT ''
PRINT '======================================================'
PRINT 'Idempotent schema sync complete.'
PRINT ''
PRINT 'Tables synced:'
PRINT '  1. FLRTTenantCredentials'
PRINT '  2. FLRTReportDefinition'
PRINT '  3. FLRTReportLineItem'
PRINT '  4. FLRTFinancialReport'
PRINT '  5. FLRTReportDefinitionLink'
PRINT '  6. FLRTPresentationGeneration'
PRINT '  7. FLRTPresentationDefinitionLink'
PRINT '  8. FLRTGIDataSource'
PRINT '  9. FLRTGIDataSourceColumn'
PRINT ' 10. FLRTPresentationDataSourceLink'
PRINT ''
PRINT 'Each table: created if missing, or columns added if absent.'
PRINT 'Migrations: orphaned columns dropped, column types corrected, PK rebuilt.'
PRINT 'Indexes: created if missing.'
PRINT '======================================================'
