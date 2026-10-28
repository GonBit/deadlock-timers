$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet publish src/DeadlockTimers.App -c Release -o dist
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Get-Item dist/DeadlockTimers.App.exe
"{0} ({1:N1} MB)" -f $exe.FullName, ($exe.Length / 1MB)
