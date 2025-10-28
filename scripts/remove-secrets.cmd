@echo off
REM Safe removal of local secrets/dev artifacts from git index (keeps local copies)
REM Usage: From repo root (cmd.exe): scripts\remove-secrets.cmd [/PUSH]

where git >nul 2>&1
if errorlevel 1 (
  echo ERROR: git is not available in PATH. Install git and re-run this script or run the commands manually.
  exit /b 2
)

set PUSH=0
if /I "%1"=="/PUSH" set PUSH=1

necho Files to untrack:
echo  - temp_auth.json
echo  - tmp_auth.json
echo  - Api\identity-dev.db
echo  - Api.Tests\bin\Debug\net10.0\identity-dev.db

necho.
echo Running: git rm --cached --ignore-unmatch temp_auth.json tmp_auth.json Api/identity-dev.db Api.Tests\bin\Debug\net10.0\identity-dev.db
git rm --cached --ignore-unmatch temp_auth.json tmp_auth.json Api/identity-dev.db Api.Tests\bin\Debug\net10.0\identity-dev.db

necho Adding .gitignore (if present) to staging area...
git add .gitignore 2>nul

necho Checking for staged changes...
git diff --cached --name-only >nul 2>&1
if errorlevel 1 (
  echo No staged changes to commit. If you expected files to be removed, ensure they were tracked before running this.
) else (
  echo Committing staged changes...
  git commit -m "Remove local secrets/DB from index; add .gitignore"
  if "%PUSH%"=="1" (
    echo Pushing to origin HEAD...
    git push origin HEAD
  ) else (
    echo Skipping push (run with /PUSH to push automatically).
  )
)

necho.
echo Next recommended actions:
echo  - Rotate any secrets that may have been exposed (signing keys, certs, API keys).
echo  - If these files were pushed to a remote and you need to purge them from history, use git-filter-repo or BFG (coordinate with collaborators).
echo.
echo Examples for history removal (manual, destructive):
echo  # Using git-filter-repo (recommended):
echo  git filter-repo --invert-paths --paths temp_auth.json --paths Api/identity-dev.db
echo  git push --force --all && git push --force --tags
echo.
echo  # Using BFG (easier):
echo  java -jar bfg.jar --delete-files temp_auth.json
echo  git reflog expire --expire=now --all && git gc --prune=now --aggressive && git push --force --all
echo.
echo Script finished.
exit /b 0
