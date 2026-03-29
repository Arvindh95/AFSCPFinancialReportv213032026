-- =============================================
-- Migration: GI Data Source
-- Creates FLRTGIDataSource and FLRTGIDataSourceColumn tables.
-- Run this ONCE against the target database.
-- Idempotent: safe to re-run.
-- =============================================

PRINT '======================================================'
PRINT 'Migration: GI Data Source'
PRINT '======================================================'

-- =============================================
-- 1. FLRTGIDataSource
-- =============================================
PRINT ''
PRINT '--- FLRTGIDataSource ---'

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
BEGIN
    PRINT 'FLRTGIDataSource already exists. Skipping.'
END
GO

-- =============================================
-- 2. FLRTGIDataSourceColumn
-- =============================================
PRINT ''
PRINT '--- FLRTGIDataSourceColumn ---'

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

    -- FK index for fast child lookups
    CREATE NONCLUSTERED INDEX [IX_FLRTGIDataSourceColumn_DataSourceID]
        ON [dbo].[FLRTGIDataSourceColumn] ([CompanyID] ASC, [DataSourceID] ASC, [SortOrder] ASC)

    PRINT 'FLRTGIDataSourceColumn created.'
END
ELSE
BEGIN
    PRINT 'FLRTGIDataSourceColumn already exists. Skipping.'
END
GO

PRINT ''
PRINT '======================================================'
PRINT 'Migration complete.'
PRINT '======================================================'
