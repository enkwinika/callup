USE OnCallDB;
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

-- Completion Images Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CompletionImages')
BEGIN
    CREATE TABLE CompletionImages (
        Id INT PRIMARY KEY IDENTITY(1,1),
        RequestId INT NOT NULL,
        ImagePath NVARCHAR(MAX) NOT NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (RequestId) REFERENCES ServiceRequests(Id)
    );
END
GO
