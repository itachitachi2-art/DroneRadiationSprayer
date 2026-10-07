@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" -GameDir "%~1"
exit /b %errorlevel%
