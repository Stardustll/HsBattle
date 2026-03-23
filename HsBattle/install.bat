@echo off
setlocal
cd /d "%~dp0"
set "HSBATTLE_INSTALL_SOURCE_DLL=%~1"
set "HSBATTLE_INSTALL_TARGET_DIR=%~2"
set "HSBATTLE_INSTALL_TARGET_FILE=%~3"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
exit /b 0
