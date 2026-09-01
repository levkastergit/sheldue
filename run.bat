@echo off
chcp 65001 >nul
title Расписание школы — запуск из исходников

where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo [ОШИБКА] На этом компьютере не найден .NET SDK.
    echo.
    echo Этот способ запуска ^(из исходного кода^) требует .NET 8 SDK.
    echo Скачайте и установите его отсюда:
    echo   https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    echo Либо, если .NET ставить не хотите — скачайте готовый .exe
    echo со страницы релизов репозитория ^(ничего устанавливать не нужно^):
    echo   https://github.com/levkastergit/sheldue/releases/latest
    echo.
    pause
    exit /b 1
)

echo Собираю и запускаю приложение — при первом запуске может занять минуту...
echo.
dotnet run --project "%~dp0src\SchoolSchedule.App\SchoolSchedule.App.csproj" -c Release

if errorlevel 1 (
    echo.
    echo Что-то пошло не так при сборке/запуске — текст ошибки выше.
    pause
)
