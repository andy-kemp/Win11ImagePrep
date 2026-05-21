@echo off
echo Starting WinImagePrep with Debug Console...
echo.
echo If you see this window but no application window appears,
echo please check the output below for errors.
echo.
echo ========================================
echo.

cd /d "%~dp0"
"WinImagePrep\bin\Debug\net8.0-windows\WinImagePrep.exe" --debug

echo.
echo ========================================
echo.
echo Application has closed.
echo Press any key to close this window...
pause > nul
