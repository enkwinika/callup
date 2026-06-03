USE OnCallDB;
GO

-- Update All Demo Users with PBKDF2 hash for 'Demo@1234'
UPDATE Users 
SET Password = 'ACXvcELTPHwdYY90hTcfD67fvcVTIabxv2giVVGSf2zOFCR/GJKyXGK+uFOSwKSGYQ==' 
WHERE Password = 'Demo@1234';

-- Update individual demo users who had different plain text passwords
UPDATE Users SET Password = 'ACXONh+CA53kXuC5SEXxj4oPLrPEcHvSa24Hlx2iln+HQ7TtaM+wrlddzQCpC42wwQ==' WHERE Email = 'test_customer@oncall.co.za'; -- Password123!
UPDATE Users SET Password = 'ACXONh+CA53kXuC5SEXxj4oPLrPEcHvSa24Hlx2iln+HQ7TtaM+wrlddzQCpC42wwQ==' WHERE Email = 'customer@oncall.com';

GO
