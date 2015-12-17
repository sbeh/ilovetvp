/*DROP TABLE [dbo].[Gamelogs];
CREATE TABLE [dbo].[Gamelogs] (
    [ID]               CHAR (20) NOT NULL,
    [Processed length] INT       NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);*/

DROP TABLE [dbo].[Event];
CREATE TABLE [dbo].[Event] (
    [ID]        INT      NOT NULL IDENTITY(1,1) PRIMARY KEY,
    [Character] INT      NOT NULL,
    [Timestamp] DATETIME NULL,
    [Type]      SMALLINT NOT NULL,
    [Message]   NTEXT    NULL
);

DROP TABLE [dbo].[Event_Combat];
CREATE TABLE [dbo].[Event_Combat] (
    [ID]     INT           NOT NULL PRIMARY KEY,
    [Damage] INT           NOT NULL,
    [Enemy]  NVARCHAR (80) NOT NULL,
    [Weapon] NVARCHAR (40) NOT NULL
);

DROP TABLE [dbo].[Character];
CREATE TABLE [dbo].[Character] (
    [ID]   INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
    [Name] NVARCHAR (80) NOT NULL
);
