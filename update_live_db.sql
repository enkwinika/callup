-- CallUp Database Migration Script
-- Purpose: Sync missing columns and tables to the Live environment
-- Date: 2026-05-02

-- 1. TABLES

-- Payouts Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Payouts')
BEGIN
    CREATE TABLE Payouts (
        Id INT PRIMARY KEY IDENTITY(1,1),
        RequestId INT NOT NULL,
        ProviderId INT NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL,
        ServiceFee DECIMAL(18,2) NOT NULL,
        PayoutAmount DECIMAL(18,2) NOT NULL,
        Status NVARCHAR(50) DEFAULT 'Pending Approval',
        CreatedAt DATETIME DEFAULT GETDATE(),
        PaidAt DATETIME NULL,
        Reference NVARCHAR(100) NULL,
        FOREIGN KEY (RequestId) REFERENCES ServiceRequests(Id),
        FOREIGN KEY (ProviderId) REFERENCES Users(Id)
    );
END
GO

-- CompletionImages Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CompletionImages')
BEGIN
    CREATE TABLE CompletionImages (
        Id INT PRIMARY KEY IDENTITY(1,1),
        RequestId INT NOT NULL,
        ImagePath NVARCHAR(MAX) NOT NULL,
        UploadedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (RequestId) REFERENCES ServiceRequests(Id)
    );
END
GO

-- RequestImages Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RequestImages')
BEGIN
    CREATE TABLE RequestImages (
        Id INT PRIMARY KEY IDENTITY(1,1),
        RequestId INT NOT NULL,
        ImagePath NVARCHAR(MAX) NOT NULL,
        UploadedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (RequestId) REFERENCES ServiceRequests(Id)
    );
END
GO

-- Notifications Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
BEGIN
    CREATE TABLE Notifications (
        Id INT PRIMARY KEY IDENTITY(1,1),
        UserId INT NOT NULL,
        RequestId INT NULL,
        Message NVARCHAR(MAX) NOT NULL,
        IsRead BIT DEFAULT 0,
        CreatedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END
GO

-- 2. MISSING COLUMNS (USERS)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsApproved')
    ALTER TABLE Users ADD IsApproved BIT NOT NULL DEFAULT (0);

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ApprovedAt')
    ALTER TABLE Users ADD ApprovedAt DATETIME NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'About')
    ALTER TABLE Users ADD About NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'Availability')
    ALTER TABLE Users ADD Availability NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ServiceRadius')
    ALTER TABLE Users ADD ServiceRadius INT DEFAULT 25;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'AccountType')
    ALTER TABLE Users ADD AccountType NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsInstantPayment')
    ALTER TABLE Users ADD IsInstantPayment BIT DEFAULT (0);

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'BankName')
    ALTER TABLE Users ADD BankName NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'AccountNumber')
    ALTER TABLE Users ADD AccountNumber NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'BranchCode')
    ALTER TABLE Users ADD BranchCode NVARCHAR(20) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'Category')
    ALTER TABLE Users ADD Category NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'StatusReason')
    ALTER TABLE Users ADD StatusReason NVARCHAR(MAX) NULL;

-- 3. MISSING COLUMNS (SERVICE REQUESTS)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ServiceRequests') AND name = 'Title')
    ALTER TABLE ServiceRequests ADD Title NVARCHAR(256) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ServiceRequests') AND name = 'CompletionNotes')
    ALTER TABLE ServiceRequests ADD CompletionNotes NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ServiceRequests') AND name = 'PriceRange')
    ALTER TABLE ServiceRequests ADD PriceRange NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ServiceRequests') AND name = 'ViewCount')
    ALTER TABLE ServiceRequests ADD ViewCount INT DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ServiceRequests') AND name = 'Latitude')
    ALTER TABLE ServiceRequests ADD Latitude FLOAT NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ServiceRequests') AND name = 'Longitude')
    ALTER TABLE ServiceRequests ADD Longitude FLOAT NULL;

-- 4. DATA SEEDING
IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Plumbing') INSERT INTO Categories (Name, Icon) VALUES ('Plumbing', 'fa-faucet');
IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Electrical') INSERT INTO Categories (Name, Icon) VALUES ('Electrical', 'fa-bolt');
IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Cleaning') INSERT INTO Categories (Name, Icon) VALUES ('Cleaning', 'fa-broom');
IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Gardening') INSERT INTO Categories (Name, Icon) VALUES ('Gardening', 'fa-leaf');
IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Painting') INSERT INTO Categories (Name, Icon) VALUES ('Painting', 'fa-paint-roller');
IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Security') INSERT INTO Categories (Name, Icon) VALUES ('Security', 'fa-shield-halved');
GO

PRINT 'Live Database Sync Completed Successfully.';
