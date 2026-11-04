@echo off
rem Avvia Privacy Studio in ambiente di sviluppo. Opzioni: -Reset (ricrea i dati dimostrativi), -NoLaunch
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Avvia-PrivacyStudio.ps1" %*
if errorlevel 1 pause
