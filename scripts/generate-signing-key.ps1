<#
Generates a 32-byte (256-bit) base64 signing key and prints it to stdout.
Usage:
  pwsh scripts/generate-signing-key.ps1
#>

$bytes = New-Object byte[] 32
$rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
$rng.GetBytes($bytes)
$base64 = [System.Convert]::ToBase64String($bytes)
Write-Output $base64
