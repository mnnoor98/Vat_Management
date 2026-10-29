@echo off
title Enterprise VAT Management - Frontend UI
color 0A
echo ============================================================
echo   Starting Enterprise VAT Management - Frontend UI
echo ============================================================
echo.
echo URL: http://localhost:4200
echo.

cd /d "%~dp0Vat_Fontend"

:: Start Angular Development Server
call npm start

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Frontend failed to start.
    pause
)
