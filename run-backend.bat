@echo off
title Enterprise VAT Management - Backend Services
color 0B
echo ============================================================
echo   Starting Enterprise VAT Management - Backend Services
echo ============================================================
echo.
echo   API Gateway: http://localhost:5080
echo   Swagger UI:  http://localhost:5080/swagger
echo   Scalar Docs: http://localhost:5080/scalar/v1
echo.

cd /d "%~dp0VAt_Asper_backend"

:: Set local development defaults if not explicitly configured
if "%VAT_OIDC_AUTHORITY%"=="" set VAT_OIDC_AUTHORITY=https://localhost:5080
if "%VAT_DB_USERNAME%"=="" set VAT_DB_USERNAME=postgres
if "%VAT_DB_PASSWORD%"=="" (
    echo [ERROR] Set VAT_DB_PASSWORD before starting backend services.
    pause
    exit /b 1
)

:: Run PowerShell script that launches Gateway and all 12 Microservices
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0VAt_Asper_backend\scripts\Run-AllBackend.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Backend failed to start.
    pause
)
