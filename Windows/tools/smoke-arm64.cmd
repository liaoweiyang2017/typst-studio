@echo off
setlocal
set "FIXTURE=%TEMP%\TypstStudio-qa-arm64"
mkdir "%FIXTURE%" 2>nul
copy /y C:\TypstStudio-build\Windows\tests\Fixture\*.typ "%FIXTURE%\" >nul
del /q "%FIXTURE%\native-smoke.json" "%FIXTURE%\native-smoke.png" 2>nul
start "" C:\TypstStudio-build\Windows\artifacts\win-arm64\TypstStudio.App.exe --smoke "%FIXTURE%" --smoke-exit >nul 2>&1
