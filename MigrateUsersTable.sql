USE OnCallDB;
GO

-- Add missing columns to Users table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'Latitude')
    ALTER TABLE Users ADD Latitude FLOAT NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'Longitude')
    ALTER TABLE Users ADD Longitude FLOAT NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'CategoryId')
    ALTER TABLE Users ADD CategoryId INT NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'PhoneNumber')
    ALTER TABLE Users ADD PhoneNumber NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsVerified')
    ALTER TABLE Users ADD IsVerified BIT NOT NULL DEFAULT 0;

-- Update existing users to be verified by default for the demo
UPDATE Users SET IsVerified = 1;

GO
