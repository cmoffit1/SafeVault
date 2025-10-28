Write-Output 'Starting API background...'
$p = Start-Process -FilePath 'powershell' -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','scripts\\run-api-https.ps1' -PassThru
Write-Output "Started process Id: $($p.Id)"
Start-Sleep -Milliseconds 900
Get-NetTCPConnection -LocalPort 7067,5046 -ErrorAction SilentlyContinue | Select-Object LocalAddress,LocalPort,State,OwningProcess | Format-List
if (-not (Get-NetTCPConnection -LocalPort 7067 -ErrorAction SilentlyContinue)) { Write-Output 'No listener on 7067 yet' } else { Write-Output 'API HTTPS listener detected' }
