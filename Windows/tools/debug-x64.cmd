@echo off
setlocal
cd /d "%~dp0.."
dotnet build src\TypstStudio.App\TypstStudio.App.csproj -c Debug -r win-x64 -p:Platform=x64 -p:PlatformTarget=x64 -p:UseSharedCompilation=false --disable-build-servers
exit /b %errorlevel%
