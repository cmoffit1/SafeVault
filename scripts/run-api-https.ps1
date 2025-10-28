$Pfx = Join-Path $env:USERPROFILE '.aspnet\https\safevault-dev.pfx'
Write-Output "Using PFX: $Pfx"
$env:ASPNETCORE_URLS = 'https://localhost:5046'
$env:Kestrel__Certificates__Default__Path = $Pfx
$env:Kestrel__Certificates__Default__Password = 'DevCertPwd123!'
# Start the API in this process so the environment variables are inherited by Kestrel
dotnet run --project Api/Api.csproj --no-launch-profile
