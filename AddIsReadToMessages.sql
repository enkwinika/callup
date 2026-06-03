-- Migration: Add IsRead column to Messages table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Messages') AND name = 'IsRead')
BEGIN
    ALTER TABLE [dbo].[Messages] ADD [IsRead] BIT NOT NULL DEFAULT 0;
END
GO
