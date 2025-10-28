# Safe removal of local secrets/dev artifacts from git index (keeps local copies)
# Usage: Run from repository root in PowerShell
# Example: powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\remove-secrets.ps1

param(
    [switch]$Push
)

function Ensure-Git {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Error "git is not available in PATH. Install git and re-run this script or run the commands manually."
        exit 2
    }
}

Ensure-Git

$filesToUntrack = @(
    'temp_auth.json',
    'tmp_auth.json',
    'Api/identity-dev.db',
    'Api.Tests/bin/Debug/net10.0/identity-dev.db'
)

Write-Host "Files to untrack:"
$filesToUntrack | ForEach-Object { Write-Host " - $_" }

# Ensure .gitignore exists in repo root
if (-not (Test-Path '.gitignore')) {
    Write-Host "No .gitignore found in repo root. Creating a minimal .gitignore and adding recommended patterns..."
    @"
# Local dev files
temp_auth.json
tmp_auth.json
Api/identity-dev.db
Api.Tests/bin/Debug/net10.0/identity-dev.db
*.db
*.sqlite

# IDE and build
.vs/
.vscode/
**/bin/
**/obj/
Client/publish/
Tools/ClientHost/publish/
"@ | Out-File -FilePath .gitignore -Encoding utf8
    git add .gitignore
} else {
    Write-Host ".gitignore already present. Will keep it and ensure changes are staged if needed."
    git add .gitignore 2>$null | Out-Null
}

# Untrack files (safe: --ignore-unmatch avoids errors if files aren't tracked)
foreach ($f in $filesToUntrack) {
    Write-Host "git rm --cached --ignore-unmatch $f"
    git rm --cached --ignore-unmatch $f
}

# Commit
$commitMsg = "Remove local secrets/DB from index; add .gitignore"
Write-Host "git commit -m '$commitMsg'"
# Only commit if there are staged changes
$staged = git diff --cached --name-only
if (-not [string]::IsNullOrEmpty($staged)) {
    git commit -m "$commitMsg"
    Write-Host "Committed."
    if ($Push) {
        Write-Host "Pushing to origin HEAD..."
        git push origin HEAD
    } else {
        Write-Host "Skipping push (use -Push to push the commit)."
    }
} else {
    Write-Host "No staged changes to commit. If you expected files to be removed, ensure they were tracked."
}

Write-Host "\nNext recommended actions:" 
Write-Host " - If these files were ever pushed to a remote, rotate any secrets that may have been exposed (signing keys, certs, API keys)."
Write-Host " - To remove files from remote history, consider using 'git filter-repo' or BFG (see comments in this script)."

Write-Host "\nExamples for history removal (manual, destructive, coordinate with collaborators):\n"
Write-Host "# Using git-filter-repo (recommended):"
Write-Host "# git filter-repo --invert-paths --paths temp_auth.json --paths Api/identity-dev.db"
Write-Host "# git push --force --all && git push --force --tags"

Write-Host "# Using BFG (easier for common patterns):"
Write-Host "# java -jar bfg.jar --delete-files temp_auth.json"
Write-Host "# git reflog expire --expire=now --all && git gc --prune=now --aggressive && git push --force --all"

Write-Host "\nScript finished."
