@echo off
title Enterprise VAT Management - Stop All Services
color 0C
echo ============================================================
echo   Stopping All Enterprise VAT Services (Backend & Frontend)
echo ============================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$ports = @(4200, 5080, 5101, 5102, 5103, 5104, 5105, 5106, 5107, 5108, 5109, 5110, 5111, 5112); " ^
    "$conns = Get-NetTCPConnection -LocalPort $ports -ErrorAction SilentlyContinue; " ^
    "if ($conns) { " ^
    "    $pids = $conns | Select-Object -ExpandProperty OwningProcess -Unique; " ^
    "    foreach ($p in $pids) { " ^
    "        if ($p -and $p -ne 0) { " ^
    "            Write-Host ('Killing PID: ' + $p) -ForegroundColor Yellow; " ^
    "            Stop-Process -Id $p -Force -ErrorAction SilentlyContinue; " ^
    "        } " ^
    "    } " ^
    "} else { Write-Host 'No active processes found on VAT ports.' -ForegroundColor Green; }"

echo.
echo ============================================================
echo   All VAT Services Have Been Stopped.
echo ============================================================
echo.
pause
