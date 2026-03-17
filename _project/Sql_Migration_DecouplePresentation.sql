-- =============================================
-- Migration: Decouple Presentation from Financial Report
-- Run this ONCE against the target database.
-- Idempotent: safe to re-run.
-- =============================================

PRINT '======================================================'
PRINT 'Migration: Decouple Presentation from Financial Report'
PRINT '======================================================'

-- =============================================
-- 1. Drop presentation columns from FLRTFinancialReport
-- =============================================
PRINT ''
PRINT '--- FLRTFinancialReport: dropping presentation columns ---'

-- PresentationTitle
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'PresentationTitle')
BEGIN
    ALTER TABLE [dbo].[FLRTFinancialReport] DROP COLUMN [PresentationTitle]
    PRINT '  - PresentationTitle dropped'
END

-- PresentationDescription
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'PresentationDescription')
BEGIN
    ALTER TABLE [dbo].[FLRTFinancialReport] DROP COLUMN [PresentationDescription]
    PRINT '  - PresentationDescription dropped'
END

-- GammaTemplateId
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'GammaTemplateId')
BEGIN
    ALTER TABLE [dbo].[FLRTFinancialReport] DROP COLUMN [GammaTemplateId]
    PRINT '  - GammaTemplateId dropped'
END

-- SlideGeneratedFileID
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'SlideGeneratedFileID')
BEGIN
    ALTER TABLE [dbo].[FLRTFinancialReport] DROP COLUMN [SlideGeneratedFileID]
    PRINT '  - SlideGeneratedFileID dropped'
END

-- PresentationMarkdown
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'PresentationMarkdown')
BEGIN
    ALTER TABLE [dbo].[FLRTFinancialReport] DROP COLUMN [PresentationMarkdown]
    PRINT '  - PresentationMarkdown dropped'
END

-- SlideStatus (has a default constraint — must drop it first)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'SlideStatus')
BEGIN
    DECLARE @constraintName NVARCHAR(256)
    SELECT @constraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE c.object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND c.name = 'SlideStatus'

    IF @constraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [dbo].[FLRTFinancialReport] DROP CONSTRAINT [' + @constraintName + ']')
        PRINT '  - SlideStatus default constraint dropped'
    END

    ALTER TABLE [dbo].[FLRTFinancialReport] DROP COLUMN [SlideStatus]
    PRINT '  - SlideStatus dropped'
END

PRINT 'FLRTFinancialReport presentation columns removed.'
GO

-- =============================================
-- 2. FLRTPresentationGeneration
-- =============================================
PRINT ''
PRINT '--- FLRTPresentationGeneration ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTPresentationGeneration...'

    CREATE TABLE [dbo].[FLRTPresentationGeneration] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [PresentationID]         [int]              IDENTITY(1,1) NOT NULL,
        [PresentationCD]         [nvarchar](225)    NULL,
        [Description]            [nvarchar](50)     NULL,
        [CurrYear]               [nvarchar](4)      NULL,
        [FinancialMonth]         [nvarchar](2)      NULL DEFAULT('12'),
        [Branch]                 [nvarchar](10)     NULL,
        [Organization]           [nvarchar](50)     NULL,
        [Ledger]                 [nvarchar](20)     NULL,
        [PresentationTitle]      [nvarchar](500)    NULL,
        [PresentationDescription][nvarchar](2000)   NULL,
        [GammaTemplateId]        [nvarchar](100)    NULL,
        [SlideGeneratedFileID]   [uniqueidentifier] NULL,
        [PresentationMarkdown]   [nvarchar](max)    NULL,
        [SlideStatus]            [nvarchar](1)      NULL DEFAULT('N'),
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
    PRINT 'FLRTPresentationGeneration already exists. Skipping.'
END
GO

-- =============================================
-- 3. FLRTPresentationDefinitionLink
-- =============================================
PRINT ''
PRINT '--- FLRTPresentationDefinitionLink ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationDefinitionLink]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTPresentationDefinitionLink...'

    CREATE TABLE [dbo].[FLRTPresentationDefinitionLink] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [LinkID]                 [int]              IDENTITY(1,1) NOT NULL,
        [PresentationID]         [int]              NULL,
        [DefinitionID]           [int]              NULL,
        [DisplayOrder]           [int]              NULL DEFAULT(0),
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
    PRINT 'FLRTPresentationDefinitionLink already exists. Skipping.'
END
GO

PRINT ''
PRINT '======================================================'
PRINT 'Migration complete.'
PRINT '======================================================'
