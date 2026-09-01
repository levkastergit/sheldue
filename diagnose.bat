@echo off
title SchoolSchedule diagnostics

set LOG=%~dp0diagnose-log.txt

echo Run date: %DATE% %TIME% > "%LOG%"
echo Computer: %COMPUTERNAME%, user: %USERNAME% >> "%LOG%"
echo. >> "%LOG%"

echo Folder contents: >> "%LOG%"
dir "%~dp0" /b >> "%LOG%"
echo. >> "%LOG%"

echo Starting SchoolSchedule.App.exe - close its window when you are done.
echo.

"%~dp0SchoolSchedule.App.exe" >> "%LOG%" 2>&1
echo. >> "%LOG%"
echo EXIT CODE: %ERRORLEVEL% >> "%LOG%"

echo.
echo ============================================================
echo Done. Result saved next to this file as diagnose-log.txt
echo.
type "%LOG%"
echo ============================================================
echo.
echo Copy the text above, or send the diagnose-log.txt file itself
echo to the developer.
echo.
pause
