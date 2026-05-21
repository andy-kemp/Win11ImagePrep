@echo off
echo.
echo ========================================
echo  Windows Image Preparation Tool
echo ========================================
echo.
echo Starting WinImagePrep as Administrator...
echo.

cd /d "%~dp0WinImagePrep\bin\Release\net8.0-windows"
powershell -Command "Start-Process -FilePath 'WinImagePrep.exe' -Verb RunAs"

echo.
echo Application launched!
echo You can close this window.
echo.
