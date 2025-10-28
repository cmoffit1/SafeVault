<#
Safe migration runner for SQL Server. Prompts for connection parameters, warns about backups, and runs the migration script.
Usage: run from project root in PowerShell
> .\scripts\run-migration.ps1
#>

param()

$scriptPath = Join-Path -Path $PSScriptRoot -ChildPath "..\DB\migrations\20251023_normalize_roles.sql"
$scriptPath = [System.IO.Path]::GetFullPath($scriptPath)
if (-not (Test-Path $scriptPath)) { Write-Error "Migration script not found at $scriptPath"; exit 1 }

Write-Host "Migration script found: $scriptPath" -ForegroundColor Cyan
Write-Host "*** BACKUP WARNING ***"
Write-Host "Make sure you have a FULL backup of the target database before proceeding." -ForegroundColor Yellow

$ok = Read-Host "Proceed? Type 'yes' to continue"
if ($ok -ne 'yes') { Write-Host "Aborted"; exit 0 }

# Prompt for connection details or allow using integrated security
$useIntegrated = Read-Host "Use Integrated Security / Windows Authentication? (y/n)"
$server = Read-Host "SQL Server (e.g. localhost\\SQLEXPRESS or mydbserver.database.windows.net)"
$db = Read-Host "Database name"

if ($useIntegrated -eq 'y') {
    $connArgs = "-S `"$server`" -d `"$db`" -E"
} else {
    $user = Read-Host "SQL Username"
    $pw = Read-Host "SQL Password" -AsSecureString
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($pw))
    $connArgs = "-S `"$server`" -d `"$db`" -U `"$user`" -P `"$plain`""
}

# Execute with sqlcmd
$sqlcmd = "sqlcmd"
if (-not (Get-Command $sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Error "sqlcmd not found in PATH. Install SQL Server command-line tools or run the script in an environment that has sqlcmd."; exit 2
}

Write-Host "Running migration..." -ForegroundColor Green
$cmd = "$sqlcmd $connArgs -i `"$scriptPath`""
Write-Host $cmd

try {
    & $sqlcmd @($connArgs.Split(' ')) -i $scriptPath
    if ($LASTEXITCODE -eq 0) { Write-Host "Migration completed successfully." -ForegroundColor Green }
    else { Write-Error "Migration failed with exit code $LASTEXITCODE" }
} catch {
    Write-Error "Error while executing migration: $_"
}
