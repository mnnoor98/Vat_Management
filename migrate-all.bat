@echo off
title Enterprise VAT Management - Database Migrator
color 0D
echo ============================================================
echo   Running All 12 Microservice Database Migrations
echo ============================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0VAt_Asper_backend\scripts\Migrate-AllDatabases.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Database migration failed.
) else (
    echo.
    echo [SUCCESS] All 12 microservice databases are migrated and verified.
)
echo.
pause
