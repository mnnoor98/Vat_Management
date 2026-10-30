@echo off
title Push to GitHub - Vat_Management
color 0B
echo ============================================================
echo   Pushing Vat_Management to GitHub (mnnoor98/Vat_Management)
echo ============================================================
echo.

cd /d "%~dp0"

echo Running: git push -u origin main
git push -u origin main

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [NOTE] If you haven't created the repository on GitHub yet:
    echo 1. Go to https://github.com/new
    echo 2. Set Repository name: Vat_Management
    echo 3. Click 'Create repository' (Do not add README)
    echo 4. Run this batch file again!
) else (
    echo.
    echo ============================================================
    echo   Successfully pushed to https://github.com/mnnoor98/Vat_Management
    echo ============================================================
)

echo.
pause

