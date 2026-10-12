@echo off
REM ECOM AE ASP.NET on-premises setup. Double-click this file (same as setup.exe).
cd /d "%~dp0"
if exist "setup.exe" (
  "setup.exe" %*
  goto :eof
)
where powershell >nul 2>nul
if errorlevel 1 (
  echo PowerShell is required. Install Docker Desktop, then run setup.cmd again.
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1" %*
