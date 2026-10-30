@echo off
title Enterprise VAT Management - Schema Migration Plan
cd /d "%~dp0"
echo Active persistence owner schema migration plan:
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0VAt_Asper_backend\scripts\Migrate-AllDatabases.ps1"
if errorlevel 1 (
    echo [ERROR] Could not prepare the schema migration plan.
    exit /b 1
)
echo.
echo To apply, configure the listed migration-owner environment settings and run the PowerShell script with -Apply.
echo This plan does not migrate business data between databases.
pause
