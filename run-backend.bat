@echo off
title Enterprise VAT Management - Backend Services
color 0B
echo ============================================================
echo   Starting Enterprise VAT Management - Backend Services
echo ============================================================
echo.

cd /d "%~dp0VAt_Asper_backend"

:: Run PowerShell script that launches Gateway and all 12 Microservices
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0VAt_Asper_backend\scripts\Run-AllBackend.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Backend failed to start.
    pause
)
