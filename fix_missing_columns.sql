USE [OnCallDB];
GO

-- Add missing columns to Users table
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ProfileImagePath' AND Object_ID = Object_ID(N'Users'))
BEGIN
    ALTER TABLE [Users] ADD [ProfileImagePath] NVARCHAR(500) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'BankName' AND Object_ID = Object_ID(N'Users'))
BEGIN
    ALTER TABLE [Users] ADD [BankName] NVARCHAR(200) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'AccountNumber' AND Object_ID = Object_ID(N'Users'))
BEGIN
    ALTER TABLE [Users] ADD [AccountNumber] NVARCHAR(100) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'BranchCode' AND Object_ID = Object_ID(N'Users'))
BEGIN
    ALTER TABLE [Users] ADD [BranchCode] NVARCHAR(50) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IsInstantPayment' AND Object_ID = Object_ID(N'Users'))
BEGIN
    ALTER TABLE [Users] ADD [IsInstantPayment] BIT NOT NULL DEFAULT (0);
END

-- Add missing columns to ServiceRequests table
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ViewCount' AND Object_ID = Object_ID(N'ServiceRequests'))
BEGIN
    ALTER TABLE [ServiceRequests] ADD [ViewCount] INT NOT NULL DEFAULT (0);
END
GO
