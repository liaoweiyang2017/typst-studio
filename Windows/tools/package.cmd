@echo off
setlocal
cd /d C:\TypstStudio-build\Windows
C:\Modernterm-tools\InnoSetup\ISCC.exe /DSourceDirectory=C:\TypstStudio-build\Windows\artifacts\win-arm64 /DTargetArchitecture=arm64 installer\TypstStudio.iss
if errorlevel 1 exit /b 1
C:\Modernterm-tools\InnoSetup\ISCC.exe /DSourceDirectory=C:\TypstStudio-build\Windows\artifacts\win-x64 /DTargetArchitecture=x64 installer\TypstStudio.iss
if errorlevel 1 exit /b 1
