@echo off
title Enterprise VAT Management - Database Schema Migration
color 0B
echo ============================================================
echo   Enterprise VAT Management - Database Schema Migration
echo ============================================================
echo.

cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    ". '%~dp0VAt_Asper_backend\scripts\Import-LocalConnections.ps1' -IncludeIdentityOwner -IncludeWorkflowOwner -IncludeComplianceOwner -IncludeManufacturingOwner -IncludeIntegrationOwner;" ^
    "$env:ConnectionStrings__VatMigrationDatabase = 'Host=127.0.0.1;Port=5432;Database=vat_management;Username=postgres;Password=123456;Include Error Detail=true;';" ^
    "& '%~dp0VAt_Asper_backend\scripts\Migrate-AllDatabases.ps1' -Apply -Configuration Debug"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Database migration failed.
    pause
    exit /b 1
)

echo.
echo ============================================================
echo   All 6 persistence owners migrated successfully!
echo ============================================================
pause
