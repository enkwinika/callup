-- Create OnCall Database
-- USE master;
-- IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'OnCallDB')
-- CREATE DATABASE OnCallDB;
-- GO
-- USE OnCallDB;
-- GO

-- Simplified Users Table (Replaces AspNet Identity)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE [dbo].[Users](
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Email] NVARCHAR(256) NOT NULL UNIQUE,
        [Password] NVARCHAR(MAX) NOT NULL,
        [FullName] NVARCHAR(256) NOT NULL,
        [UserRole] NVARCHAR(50) NOT NULL, -- Customer, Provider, Admin
        [IdNumber] NVARCHAR(50) NULL,
        [CompanyName] NVARCHAR(256) NULL,
        [CompanyRegNo] NVARCHAR(100) NULL,
        [Category] NVARCHAR(100) NULL,
        [CategoryId] INT NULL,
        [VerificationCode] NVARCHAR(10) NULL,
        [VerificationExpiry] DATETIME NULL,
        [IsVerified] BIT DEFAULT 0,
        [IsActive] BIT DEFAULT 1,
        [StatusReason] NVARCHAR(MAX) NULL,
        [EscrowWallet] DECIMAL(18, 2) DEFAULT 0,
        [BankName] NVARCHAR(100) NULL,
        [AccountNumber] NVARCHAR(50) NULL,
        [BranchCode] NVARCHAR(20) NULL,
        [IsInstantPayment] BIT DEFAULT (0),
        [IsApproved] BIT NOT NULL DEFAULT (0),
        [ApprovedAt] DATETIME NULL,
        [PhoneNumber] NVARCHAR(20) NULL,
        [Location] NVARCHAR(MAX) NULL,
        [ProfileImagePath] NVARCHAR(MAX) NULL,
        [Latitude] FLOAT NULL,
        [Longitude] FLOAT NULL,
        [CreatedAt] DATETIME DEFAULT GETDATE()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AspNetUserLogins')
BEGIN
    CREATE TABLE [dbo].[AspNetUserLogins](
        [LoginProvider] [nvarchar](128) NOT NULL,
        [ProviderKey] [nvarchar](128) NOT NULL,
        [UserId] [nvarchar](128) NOT NULL,
        CONSTRAINT [PK_dbo.AspNetUserLogins] PRIMARY KEY CLUSTERED ([LoginProvider] ASC, [ProviderKey] ASC, [UserId] ASC)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AspNetUserClaims')
BEGIN
    CREATE TABLE [dbo].[AspNetUserClaims](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [UserId] [nvarchar](128) NOT NULL,
        [ClaimType] [nvarchar](max) NULL,
        [ClaimValue] [nvarchar](max) NULL,
        CONSTRAINT [PK_dbo.AspNetUserClaims] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END

-- Categories Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Categories')
BEGIN
    CREATE TABLE Categories (
        Id INT PRIMARY KEY IDENTITY(1,1),
        Name NVARCHAR(100) NOT NULL UNIQUE,
        Icon NVARCHAR(50)
    );
END
GO

-- Service Requests Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ServiceRequests')
BEGIN
    CREATE TABLE ServiceRequests (
        Id INT PRIMARY KEY IDENTITY(1,1),
        CustomerID INT NOT NULL,
        CategoryID INT NOT NULL,
        Title NVARCHAR(256) NULL,
        Description NVARCHAR(MAX),
        Location NVARCHAR(255),
        SpecialNotes NVARCHAR(MAX) NULL,
        CompletionNotes NVARCHAR(MAX) NULL,
        Latitude FLOAT,
        Longitude FLOAT,
        Status NVARCHAR(50) DEFAULT 'Moderation',
        PriceRange NVARCHAR(100) NULL,
        ViewCount INT DEFAULT 0,
        CreatedAt DATETIME DEFAULT GETDATE(),
        ServiceDate DATETIME NULL,
        ExpiresAt DATETIME,
        WorkOrderDate DATETIME NULL,
        CompletedAt DATETIME NULL,
        SelectedProviderId INT NULL,
        FOREIGN KEY (CustomerID) REFERENCES Users(Id),
        FOREIGN KEY (CategoryID) REFERENCES Categories(Id)
    );
END
GO

-- Ratings Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Ratings')
BEGIN
    CREATE TABLE Ratings (
        Id INT PRIMARY KEY IDENTITY(1,1),
        RequestId INT NOT NULL,
        FromCustomerId INT NOT NULL,
        ToProviderId INT NOT NULL,
        Score INT NOT NULL CHECK (Score BETWEEN 1 AND 5),
        Comment NVARCHAR(MAX),
        CreatedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (RequestId) REFERENCES ServiceRequests(Id),
        FOREIGN KEY (FromCustomerId) REFERENCES Users(Id),
        FOREIGN KEY (ToProviderId) REFERENCES Users(Id)
    );
END
GO

-- Bids Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Bids')
BEGIN
    CREATE TABLE Bids (
        Id INT PRIMARY KEY IDENTITY(1,1),
        RequestID INT NOT NULL,
        ProviderID INT NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        ETA NVARCHAR(100),
        Notes NVARCHAR(MAX) NULL,
        Latitude FLOAT,
        Longitude FLOAT,
        Status NVARCHAR(50) DEFAULT 'Pending',
        CreatedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (RequestID) REFERENCES ServiceRequests(Id),
        FOREIGN KEY (ProviderID) REFERENCES Users(Id)
    );
END
GO

-- ShieldPayments (Escrow) Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ShieldPayments')
BEGIN
    CREATE TABLE ShieldPayments (
        Id INT PRIMARY KEY IDENTITY(1,1),
        BidID INT NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        PenaltyShield BIT DEFAULT 0,
        Status NVARCHAR(50) DEFAULT 'Held',
        CreatedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (BidID) REFERENCES Bids(Id)
    );
END
GO

-- Providers Verification Documents
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProviderDocuments')
BEGIN
    CREATE TABLE ProviderDocuments (
        Id INT PRIMARY KEY IDENTITY(1,1),
        UserId INT NOT NULL,
        DocumentType NVARCHAR(50) NOT NULL,
        FilePath NVARCHAR(MAX) NOT NULL,
        Status NVARCHAR(50) DEFAULT 'Pending',
        UploadedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END
GO

-- Activity Logs Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ActivityLogs')
BEGIN
    CREATE TABLE ActivityLogs (
        Id INT PRIMARY KEY IDENTITY(1,1),
        UserId INT NOT NULL,
        Action NVARCHAR(100) NOT NULL,
        Details NVARCHAR(MAX),
        IPAddress NVARCHAR(50),
        CreatedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END
GO

-- Seed Categories (Idempotent)
IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Plumbing') INSERT INTO Categories (Name, Icon) VALUES ('Plumbing', 'fa-faucet');
IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Electrical') INSERT INTO Categories (Name, Icon) VALUES ('Electrical', 'fa-bolt');
IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Cleaning') INSERT INTO Categories (Name, Icon) VALUES ('Cleaning', 'fa-broom');
IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Gardening') INSERT INTO Categories (Name, Icon) VALUES ('Gardening', 'fa-leaf');
IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Painting') INSERT INTO Categories (Name, Icon) VALUES ('Painting', 'fa-paint-roller');
IF NOT EXISTS (SELECT * FROM Categories WHERE Name = 'Security') INSERT INTO Categories (Name, Icon) VALUES ('Security', 'fa-shield-halved');
GO

-- Completion Images Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CompletionImages')
BEGIN
    CREATE TABLE [dbo].[CompletionImages](
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [RequestId] INT NOT NULL,
        [ImagePath] NVARCHAR(MAX) NOT NULL,
        [UploadedAt] DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (RequestId) REFERENCES ServiceRequests(Id)
    );
END
GO

-- Request Images Table (Before Moderation)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RequestImages')
BEGIN
    CREATE TABLE [dbo].[RequestImages](
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [RequestId] INT NOT NULL,
        [ImagePath] NVARCHAR(MAX) NOT NULL,
        [UploadedAt] DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (RequestId) REFERENCES ServiceRequests(Id)
    );
END
GO

-- Error Logs Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ErrorLogs')
BEGIN
    CREATE TABLE ErrorLogs (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Message] NVARCHAR(MAX) NOT NULL,
        [StackTrace] NVARCHAR(MAX) NULL,
        [Controller] NVARCHAR(100) NULL,
        [Action] NVARCHAR(100) NULL,
        [UserId] INT NULL,
        [CreatedAt] DATETIME DEFAULT GETDATE()
    );
END

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
-- Password Reset Tokens Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PasswordResetTokens')
BEGIN
    CREATE TABLE [dbo].[PasswordResetTokens](
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Email] NVARCHAR(256) NOT NULL,
        [Token] NVARCHAR(50) NOT NULL,
        [Expiry] DATETIME NOT NULL,
        [Used] BIT DEFAULT 0
    );
END
-- Messages Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Messages')
BEGIN
    CREATE TABLE [dbo].[Messages](
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [SenderId] INT NOT NULL,
        [ReceiverId] INT NOT NULL,
        [Content] NVARCHAR(MAX) NOT NULL,
        [IsRead] BIT NOT NULL DEFAULT (0),
        [CreatedAt] DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (SenderId) REFERENCES Users(Id),
        FOREIGN KEY (ReceiverId) REFERENCES Users(Id)
    );
END
GO

-- Addresses Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Addresses')
BEGIN
    CREATE TABLE [dbo].[Addresses](
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [UserId] INT NOT NULL,
        [Label] NVARCHAR(100) NULL,
        [StreetAddress] NVARCHAR(256) NULL,
        [City] NVARCHAR(100) NULL,
        [Province] NVARCHAR(100) NULL,
        [PostalCode] NVARCHAR(20) NULL,
        [Latitude] FLOAT NOT NULL,
        [Longitude] FLOAT NOT NULL,
        [IsDefault] BIT DEFAULT 0,
        FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END
GO

-- Quotes Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Quotes')
BEGIN
    CREATE TABLE [dbo].[Quotes](
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [BidId] INT NOT NULL,
        [FilePath] NVARCHAR(MAX) NOT NULL,
        [Amount] DECIMAL(18,2) NOT NULL,
        [CreatedAt] DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (BidId) REFERENCES Bids(Id)
    );
END
GO

-- Identity Role Tables
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Roles')
BEGIN
    CREATE TABLE [dbo].[Roles](
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Name] NVARCHAR(256) NOT NULL UNIQUE
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserRoles')
BEGIN
    CREATE TABLE [dbo].[UserRoles](
        [UserId] INT NOT NULL,
        [RoleId] INT NOT NULL,
        PRIMARY KEY ([UserId], [RoleId]),
        FOREIGN KEY (UserId) REFERENCES Users(Id),
        FOREIGN KEY (RoleId) REFERENCES Roles(Id)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserLogins')
BEGIN
    CREATE TABLE [dbo].[UserLogins](
        [LoginProvider] NVARCHAR(128) NOT NULL,
        [ProviderKey] NVARCHAR(128) NOT NULL,
        [UserId] INT NOT NULL,
        PRIMARY KEY ([LoginProvider], [ProviderKey], [UserId]),
        FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserClaims')
BEGIN
    CREATE TABLE [dbo].[UserClaims](
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [UserId] INT NOT NULL,
        [ClaimType] NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END
GO


-- Migration: Add missing columns to Users if they don't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'CategoryId')
    ALTER TABLE Users ADD CategoryId INT NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'VerificationCode')
    ALTER TABLE Users ADD VerificationCode NVARCHAR(10) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'VerificationExpiry')
    ALTER TABLE Users ADD VerificationExpiry DATETIME NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsActive')
    ALTER TABLE Users ADD IsActive BIT DEFAULT 1;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'StatusReason')
    ALTER TABLE Users ADD StatusReason NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ProfileImagePath')
    ALTER TABLE Users ADD ProfileImagePath NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsApproved')
    ALTER TABLE Users ADD IsApproved BIT NOT NULL DEFAULT (0);

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ApprovedAt')
    ALTER TABLE Users ADD ApprovedAt DATETIME NULL;

-- Migration: Add missing columns to ServiceRequests
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ServiceRequests') AND name = 'SpecialNotes')
    ALTER TABLE ServiceRequests ADD SpecialNotes NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ServiceRequests') AND name = 'ServiceDate')
    ALTER TABLE ServiceRequests ADD ServiceDate DATETIME NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ServiceRequests') AND name = 'WorkOrderDate')
    ALTER TABLE ServiceRequests ADD WorkOrderDate DATETIME NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ServiceRequests') AND name = 'CompletedAt')
    ALTER TABLE ServiceRequests ADD CompletedAt DATETIME NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ServiceRequests') AND name = 'SelectedProviderId')
    ALTER TABLE ServiceRequests ADD SelectedProviderId INT NULL;

-- Migration: Add missing columns to Messages
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Messages') AND name = 'IsRead')
    ALTER TABLE Messages ADD IsRead BIT NOT NULL DEFAULT (0);

-- SEED DATA: Categories
IF NOT EXISTS (SELECT 1 FROM Categories WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT Categories ON;
    INSERT INTO Categories (Id, Name, Icon) VALUES
    (1, 'Plumbing', 'fa-faucet'),
    (2, 'Electrical', 'fa-bolt'),
    (3, 'Cleaning', 'fa-broom'),
    (4, 'Gardening', 'fa-leaf'),
    (5, 'Painting', 'fa-paint-roller'),
    (6, 'Security', 'fa-shield-halved'),
    (7, 'Moving', NULL),
    (8, 'Appliances', NULL);
    SET IDENTITY_INSERT Categories OFF;
END

-- SEED DATA: Users (Sync from local dev environment)
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'admin@admin.co.za')
BEGIN
    SET IDENTITY_INSERT Users ON;
    INSERT INTO Users (Id, Email, [Password], FullName, UserRole, IsVerified, EscrowWallet, CreatedAt, Location, PhoneNumber, IsApproved, IsActive)
    VALUES 
    (106, 'enkwinika@gmail.com', 'ADXBqI7cqNPYHLjDkaykBhMfIJs6RzbfYG3w9xeRT/rgeiGNx1E97E+RkU5iPUmvrA==', 'Eugine Nkwinika', 'Customer', 1, 0, '2026-04-12 18:12:20', 'Pretoria', '0768895858', 1, 1),
    (107, 'lama@mail.com', 'ADXBqI7cqNPYHLjDkaykBhMfIJs6RzbfYG3w9xeRT/rgeiGNx1E97E+RkU5iPUmvrA==', 'Lama Lama', 'Customer', 1, 0, '2026-04-12 18:22:10', 'Pretoria', '0768895858', 1, 1),
    (109, 'admin@admin.co.za', 'ADXBqI7cqNPYHLjDkaykBhMfIJs6RzbfYG3w9xeRT/rgeiGNx1E97E+RkU5iPUmvrA==', 'System Administrator', 'Admin', 1, 0, '2026-04-12 19:00:31', NULL, NULL, 1, 1);

    INSERT INTO Users (Id, Email, [Password], FullName, UserRole, CompanyName, CompanyRegNo, IsVerified, EscrowWallet, CreatedAt, Location, CategoryId, PhoneNumber, IsApproved, IsActive)
    VALUES
    (108, 'abc@gmail.com', 'ADXBqI7cqNPYHLjDkaykBhMfIJs6RzbfYG3w9xeRT/rgeiGNx1E97E+RkU5iPUmvrA==', 'ABC Plumber', 'Provider', 'ABC Plumber', '2025/12555/01', 1, 0, '2026-04-12 18:53:33', 'Pretoria', 2, '0768898585', 1, 1);
    
    SET IDENTITY_INSERT Users OFF;
END

-- SEED DATA: ServiceRequests
IF NOT EXISTS (SELECT 1 FROM ServiceRequests WHERE Id = 15)
BEGIN
    SET IDENTITY_INSERT ServiceRequests ON;
    INSERT INTO ServiceRequests (Id, CustomerID, CategoryID, Description, Location, [Status], CreatedAt, Title, SpecialNotes, PriceRange)
    VALUES 
    (15, 107, 1, 'fix my leakl', 'Pretoria', 'Closed', '2026-04-12 18:46:46', 'Fix Leak', '[Urgency: This week]', 'R500 - R1,500'),
    (16, 106, 1, 'Kitchen sink water leak', 'Pretoria', 'InProgress', '2026-04-12 19:32:54', 'Water Lean', '[Urgency: As soon as possible]', 'Under R500');
    SET IDENTITY_INSERT ServiceRequests OFF;
    
    -- Update Request 16 with its SelectedProvider
    UPDATE ServiceRequests SET SelectedProviderId = 108, ViewCount = 5 WHERE Id = 16;
END

-- SEED DATA: Bids
IF NOT EXISTS (SELECT 1 FROM Bids WHERE Id = 3)
BEGIN
    SET IDENTITY_INSERT Bids ON;
    INSERT INTO Bids (Id, RequestID, ProviderID, Amount, [Status], CreatedAt, Notes)
    VALUES (3, 16, 108, 450.00, 'Selected', '2026-04-12 21:15:47', 'change my mind');
    SET IDENTITY_INSERT Bids OFF;
END

-- SEED DATA: Messages
IF NOT EXISTS (SELECT 1 FROM Messages WHERE Id = 1)
BEGIN
    INSERT INTO Messages (SenderId, ReceiverId, Content, CreatedAt)
    VALUES 
    (109, 106, 'Welcome to CallUp! Your account is now active.', GETDATE()),
    (108, 106, 'Hi, I can assist with your kitchen leak. Please check my bid.', DATEADD(minute, -30, GETDATE()));
END
