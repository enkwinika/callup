-- Test Customers
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'customer1@test.com')
INSERT INTO Users (Email, [Password], FullName, UserRole, IsVerified, IsApproved, IsActive, CreatedAt)
VALUES ('customer1@test.com', 'ADXBqI7cqNPYHLjDkaykBhMfIJs6RzbfYG3w9xeRT/rgeiGNx1E97E+RkU5iPUmvrA==', 'Test Customer 1', 'Customer', 1, 1, 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'customer2@test.com')
INSERT INTO Users (Email, [Password], FullName, UserRole, IsVerified, IsApproved, IsActive, CreatedAt)
VALUES ('customer2@test.com', 'ADXBqI7cqNPYHLjDkaykBhMfIJs6RzbfYG3w9xeRT/rgeiGNx1E97E+RkU5iPUmvrA==', 'Test Customer 2', 'Customer', 1, 1, 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'customer3@test.com')
INSERT INTO Users (Email, [Password], FullName, UserRole, IsVerified, IsApproved, IsActive, CreatedAt)
VALUES ('customer3@test.com', 'ADXBqI7cqNPYHLjDkaykBhMfIJs6RzbfYG3w9xeRT/rgeiGNx1E97E+RkU5iPUmvrA==', 'Test Customer 3', 'Customer', 1, 1, 1, GETDATE());

-- Test Providers
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'provider1@test.com')
INSERT INTO Users (Email, [Password], FullName, UserRole, CompanyName, CategoryId, IsVerified, IsApproved, IsActive, CreatedAt)
VALUES ('provider1@test.com', 'ADXBqI7cqNPYHLjDkaykBhMfIJs6RzbfYG3w9xeRT/rgeiGNx1E97E+RkU5iPUmvrA==', 'Test Provider 1', 'Provider', 'Test Plumbing Services', 1, 1, 1, 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'provider2@test.com')
INSERT INTO Users (Email, [Password], FullName, UserRole, CompanyName, CategoryId, IsVerified, IsApproved, IsActive, CreatedAt)
VALUES ('provider2@test.com', 'ADXBqI7cqNPYHLjDkaykBhMfIJs6RzbfYG3w9xeRT/rgeiGNx1E97E+RkU5iPUmvrA==', 'Test Electrical Services', 'Provider', 'Test Electrical Services', 2, 1, 1, 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'provider3@test.com')
INSERT INTO Users (Email, [Password], FullName, UserRole, CompanyName, CategoryId, IsVerified, IsApproved, IsActive, CreatedAt)
VALUES ('provider3@test.com', 'ADXBqI7cqNPYHLjDkaykBhMfIJs6RzbfYG3w9xeRT/rgeiGNx1E97E+RkU5iPUmvrA==', 'Test Cleaning Services', 'Provider', 'Test Cleaning Services', 3, 1, 1, 1, GETDATE());
