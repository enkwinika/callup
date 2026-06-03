$pwd = "Password123!"
$salt = New-Object byte[] 16
(New-Object Security.Cryptography.RNGCryptoServiceProvider).GetBytes($salt)
$pbkdf2 = New-Object Security.Cryptography.Rfc2898DeriveBytes($pwd, $salt, 1000)
$hash = $pbkdf2.GetBytes(32)
$result = New-Object byte[] 49
$result[0] = 0
[Array]::Copy($salt, 0, $result, 1, 16)
[Array]::Copy($hash, 0, $result, 17, 32)
$base64 = [Convert]::ToBase64String($result)
Write-Output "Password123! -> $base64"

$pwd = "Demo@1234"
$salt = New-Object byte[] 16
(New-Object Security.Cryptography.RNGCryptoServiceProvider).GetBytes($salt)
$pbkdf2 = New-Object Security.Cryptography.Rfc2898DeriveBytes($pwd, $salt, 1000)
$hash = $pbkdf2.GetBytes(32)
$result = New-Object byte[] 49
$result[0] = 0
[Array]::Copy($salt, 0, $result, 1, 16)
[Array]::Copy($hash, 0, $result, 17, 32)
$base64 = [Convert]::ToBase64String($result)
Write-Output "Demo@1234 -> $base64"
