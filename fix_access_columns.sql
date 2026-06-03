USE [OnCallDB];
GO

-- Add missing IsApproved column to Users table
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IsApproved' AND Object_ID = Object_ID(N'Users'))
BEGIN
    PRINT 'Adding IsApproved column to Users table...';
    ALTER TABLE [Users] ADD [IsApproved] BIT NOT NULL DEFAULT (0);
END
ELSE
BEGIN
    PRINT 'IsApproved column already exists.';
END
GO

-- Add missing ApprovedAt column to Users table
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ApprovedAt' AND Object_ID = Object_ID(N'Users'))
BEGIN
    PRINT 'Adding ApprovedAt column to Users table...';
    ALTER TABLE [Users] ADD [ApprovedAt] DATETIME NULL;
END
ELSE
BEGIN
    PRINT 'ApprovedAt column already exists.';
END
GO

-- Ensure IsVerified is also present and correctly defaulted
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IsVerified' AND Object_ID = Object_ID(N'Users'))
BEGIN
    PRINT 'Adding IsVerified column to Users table...';
    ALTER TABLE [Users] ADD [IsVerified] BIT NOT NULL DEFAULT (0);
END
GO

-- Set all existing users to Approved for the demo so they can log in
PRINT 'Updating existing users to be Approved and Verified for the demo...';
UPDATE Users SET IsApproved = 1, IsVerified = 1 WHERE IsApproved = 0 OR IsVerified = 0;
GO

PRINT 'Schema fix complete.';
GO
