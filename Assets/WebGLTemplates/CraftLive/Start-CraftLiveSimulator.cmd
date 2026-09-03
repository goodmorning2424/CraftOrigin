@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-CraftLiveSimulator.ps1" %*
if errorlevel 1 (
  echo.
  echo CraftOrigin simulator could not start.
  pause
)
endlocal
