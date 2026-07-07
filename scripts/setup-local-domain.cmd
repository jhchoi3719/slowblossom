@echo off
chcp 65001 >nul
echo ========================================
echo  slowblossom.com local domain setup
echo  Run as Administrator
echo ========================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup-local-domain.ps1"
if errorlevel 1 (
    echo.
    echo Setup failed. Run this window as Administrator.
    pause
    exit /b 1
)

echo.
echo Start server:
echo   cd /d "%~dp0.."
echo   dotnet run --launch-profile https
echo.
echo Open: http://slowblossom.com
pause
