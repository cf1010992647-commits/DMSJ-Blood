@echo off
setlocal

cd /d "%~dp0"

where git >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Git is not installed or not in PATH.
    exit /b 1
)

git rev-parse --is-inside-work-tree >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Current folder is not a Git repository.
    exit /b 1
)

set "MSG=%~1"
if "%MSG%"=="" (
    for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "(Get-Date).ToString('yyyy-MM-dd HH:mm:ss')"`) do set "TS=%%i"
    set "MSG=auto commit %TS%"
)

echo.
echo [1/4] Staging changes...
git add -A
if errorlevel 1 (
    echo [ERROR] git add failed.
    exit /b 1
)

echo [2/4] Checking staged changes...
git diff --cached --quiet
if errorlevel 1 (
    echo [3/4] Creating commit: %MSG%
    git commit -m "%MSG%"
    if errorlevel 1 (
        echo [ERROR] git commit failed.
        exit /b 1
    )
) else (
    echo [INFO] No changes to commit.
)

echo [4/4] Pushing...
git rev-parse --abbrev-ref --symbolic-full-name @{u} >nul 2>nul
if errorlevel 1 (
    for /f "usebackq delims=" %%i in (`git branch --show-current`) do set "BRANCH=%%i"
    if "%BRANCH%"=="" set "BRANCH=main"
    git push -u origin "%BRANCH%"
) else (
    git push
)

if errorlevel 1 (
    echo [ERROR] git push failed.
    exit /b 1
)

echo.
echo [DONE] Upload completed.
exit /b 0

