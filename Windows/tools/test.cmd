@echo off
setlocal
cd /d "%~dp0.."
set "TARGETS=%~1"
if not defined TARGETS set "TARGETS=arm64 x64"
for %%A in (%TARGETS%) do (
 dotnet run --project tests\TypstStudio.Tests -c Release -r win-%%A -p:UseSharedCompilation=false
 if errorlevel 1 exit /b 1
)
