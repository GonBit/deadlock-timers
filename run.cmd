@echo off
cd /d "%~dp0"
dotnet build src\DeadlockTimers.App -c Debug -nologo -v q
if errorlevel 1 (
    echo Build failed. Install the .NET 10 SDK and try again.
    pause
    exit /b 1
)
start "" "src\DeadlockTimers.App\bin\Debug\net10.0-windows10.0.19041.0\DeadlockTimers.App.exe"
