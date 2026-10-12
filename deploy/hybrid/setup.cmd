@echo off
REM Hybrid Windows setup. Double-click this file, or use setup.exe when present.
cd /d "%~dp0"
if exist "setup.exe" (
  "setup.exe" --mode hybrid %*
  goto :eof
)
where powershell >nul 2>nul
if errorlevel 1 (
  echo PowerShell is required. Install Docker Desktop, then run setup.cmd again.
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1" %*
