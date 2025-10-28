[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
$body = @{ Username = 'admin'; Password = 'ChangeMe123!' } | ConvertTo-Json
try {
	$res = Invoke-RestMethod -Uri 'https://localhost:7067/authenticate' -Method Post -Body $body -ContentType 'application/json'
	Write-Output 'Response:'
	Write-Output ($res | ConvertTo-Json -Depth 5)
} catch {
	Write-Output 'Request failed:'
	Write-Output $_.Exception.ToString()
}
