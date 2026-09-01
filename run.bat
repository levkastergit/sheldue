@echo off
title Build and run from source

where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo [ERROR] .NET SDK was not found on this computer.
    echo.
    echo Running from source requires the .NET 8 SDK. Download it from:
    echo   https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    echo Or, if you do not want to install .NET - download the ready-made
    echo .exe from the releases page instead - it needs nothing installed:
    echo   https://github.com/levkastergit/sheldue/releases/latest
    echo.
    pause
    exit /b 1
)

echo Building and running the app - first run may take a minute...
echo.
dotnet run --project "%~dp0src\SchoolSchedule.App\SchoolSchedule.App.csproj" -c Release

if errorlevel 1 (
    echo.
    echo Something went wrong during build/run - see the error above.
    pause
)
