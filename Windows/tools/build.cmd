@echo off
setlocal
cd /d "%~dp0.."
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_GENERATE_ASPNET_CERTIFICATE=false
set "TARGETS=%~1"
if not defined TARGETS set "TARGETS=arm64 x64"
for %%A in (%TARGETS%) do (
 call :build %%A
 if errorlevel 1 exit /b 1
)
exit /b 0
:build
set "ARCH=%~1"
if "%ARCH%"=="arm64" (set PLATFORM=ARM64) else if "%ARCH%"=="x64" (set PLATFORM=x64) else exit /b 1
if not exist "native\win-%ARCH%\tinymist.exe" (echo Missing patched Tinymist helper for %ARCH%. See README.md. & exit /b 1)
if not exist "native\win-%ARCH%\pdfium.dll" (echo Missing PDFium library for %ARCH%. See README.md. & exit /b 1)
dotnet build src\TypstStudio.App\TypstStudio.App.csproj -c Release -r win-%ARCH% -p:Platform=%PLATFORM% -p:PlatformTarget=%PLATFORM% -p:UseSharedCompilation=false --no-incremental --disable-build-servers
if errorlevel 1 exit /b 1
dotnet publish src\TypstStudio.App\TypstStudio.App.csproj -c Release -r win-%ARCH% -p:Platform=%PLATFORM% -p:PlatformTarget=%PLATFORM% --no-build --no-restore -o artifacts\win-%ARCH%
if errorlevel 1 exit /b 1
del /q "artifacts\win-%ARCH%\*.pdb" 2>nul
for %%F in (TypstStudio.App.exe TypstStudio.App.pri App.xbf MainWindow.xbf tinymist.exe pdfium.dll Microsoft.UI.Xaml.dll coreclr.dll) do if not exist "artifacts\win-%ARCH%\%%F" (echo Missing publish artifact %%F & exit /b 1)
exit /b 0
