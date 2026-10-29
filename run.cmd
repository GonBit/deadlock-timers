@echo off
cd /d "%~dp0"

rem Use the system .NET 10 SDK if present, otherwise a private copy in .dotnet (no admin needed).
set "HAVE_SDK="
where dotnet >nul 2>nul
if not errorlevel 1 (
    dotnet --list-sdks 2>nul | findstr /b "10." >nul && set "HAVE_SDK=1"
)

if not defined HAVE_SDK (
    if not exist "%~dp0.dotnet\dotnet.exe" (
        echo .NET 10 SDK not found. Downloading a local copy, this takes a minute...
        powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol = 'Tls12'; & ([scriptblock]::Create((Invoke-RestMethod 'https://dot.net/v1/dotnet-install.ps1'))) -Channel 10.0 -InstallDir '%~dp0.dotnet' -NoPath"
        if errorlevel 1 (
            echo Could not install the .NET 10 SDK. Check the internet connection, or install it from https://dotnet.microsoft.com/download/dotnet/10.0
            pause
            exit /b 1
        )
    )
    set "DOTNET_ROOT=%~dp0.dotnet"
    set "PATH=%~dp0.dotnet;%PATH%"
)

set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
set "OUT=%~dp0.run"
dotnet build "src\DeadlockTimers.App" -c Debug -nologo -v q -o "%OUT%"
if errorlevel 1 (
    echo Build failed. See the errors above.
    pause
    exit /b 1
)

if not exist "%OUT%\DeadlockTimers.App.exe" (
    echo Build finished but DeadlockTimers.App.exe is missing. Antivirus or Smart App Control may have removed it.
    echo Check Windows Security protection history, allow the file, then run again.
    pause
    exit /b 1
)

echo Deadlock Timers is running. Close this window to stop it.
start "" /wait "%OUT%\DeadlockTimers.App.exe"
