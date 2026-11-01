@echo off
title Enterprise VAT Management - System Launcher
color 0E
echo ============================================================
echo   ENTERPRISE VAT MANAGEMENT SYSTEM - 1-CLICK LAUNCHER
echo ============================================================
echo.

:: Set local development defaults if not explicitly configured
if "%VAT_OIDC_AUTHORITY%"=="" set VAT_OIDC_AUTHORITY=https://localhost:5080
if "%VAT_DB_USERNAME%"=="" set VAT_DB_USERNAME=postgres
if "%VAT_DB_PASSWORD%"=="" (
    echo [ERROR] Set VAT_DB_PASSWORD before starting the system.
    pause
    exit /b 1
)

echo [1/3] Starting Backend Microservices and API Gateway...
start "Enterprise VAT - Backend" cmd /c "%~dp0run-backend.bat"

echo.
echo [2/3] Waiting 6 seconds for backend readiness...
timeout /t 6 /nobreak >nul

echo.
echo [3/3] Starting Frontend Angular Application...
start "Enterprise VAT - Frontend" cmd /c "%~dp0run-frontend.bat"

echo.
echo ============================================================
echo   All services launched!
echo   Frontend UI: http://localhost:4200
echo   API Gateway: http://localhost:5080
echo   Swagger UI:  http://localhost:5080/swagger
echo   Scalar Docs: http://localhost:5080/scalar/v1
echo ============================================================
echo.
timeout /t 3 /nobreak >nul
start http://localhost:4200
start http://localhost:5080/swagger
exit
