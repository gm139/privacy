@echo off
setlocal EnableExtensions
chcp 65001 >nul
title Privacy Studio - riporta il PC allo stato pulito
rem ============================================================================
rem  Disinstalla Privacy Studio e PostgreSQL 17 e cancella TUTTE le cartelle usate:
rem  database, backup (anche la copia esterna), chiavi, configurazione, collegamenti.
rem  SOLO PER PC DI PROVA: i dati cancellati NON sono recuperabili.
rem  Va eseguito come amministratore, con lo stesso utente che ha fatto l'installazione.
rem ============================================================================

net session >nul 2>&1
if errorlevel 1 (
  echo Questo programma va eseguito come amministratore:
  echo clic destro sul file, poi "Esegui come amministratore".
  pause
  exit /b 1
)

echo.
echo  ATTENZIONE - verra' cancellato in modo DEFINITIVO:
echo    - Privacy Studio e il suo database
echo    - PostgreSQL 17 (qualsiasi database contenga) e l'utente Windows "postgres"
echo    - backup, copia esterna, chiavi di recupero, configurazione
echo.
set /p CONF=Per procedere scrivere SI e premere Invio: 
if /i not "%CONF%"=="SI" (
  echo Annullato.
  pause
  exit /b 0
)

set "APPCFG=%LOCALAPPDATA%\PrivacyStudio\config.json"
set "BKDIR="
set "EXTDIR="
if exist "%APPCFG%" (
  for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "try{(Get-Content -Raw '%APPCFG%' | ConvertFrom-Json).backupDir}catch{}"`) do set "BKDIR=%%A"
  for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "try{(Get-Content -Raw '%APPCFG%' | ConvertFrom-Json).externalBackupDir}catch{}"`) do set "EXTDIR=%%A"
)

echo.
echo [1/8] Chiusura del programma...
taskkill /f /im Privacy.App.exe >nul 2>&1

echo [2/8] Disinstallazione di Privacy Studio...
if exist "%ProgramFiles%\PrivacyStudio\unins000.exe" (
  "%ProgramFiles%\PrivacyStudio\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
  timeout /t 3 /nobreak >nul
) else echo      (non risulta installato)

echo [3/8] Arresto del servizio PostgreSQL...
net stop postgresql-x64-17 >nul 2>&1
taskkill /f /im postgres.exe >nul 2>&1

echo [4/8] Disinstallazione di PostgreSQL 17...
if exist "%ProgramFiles%\PostgreSQL\17\uninstall-postgresql.exe" (
  "%ProgramFiles%\PostgreSQL\17\uninstall-postgresql.exe" --mode unattended --unattendedmodeui none
  timeout /t 5 /nobreak >nul
) else echo      (disinstallatore non trovato)
sc query postgresql-x64-17 >nul 2>&1 && sc delete postgresql-x64-17 >nul 2>&1

echo [5/8] Rimozione dell'utente Windows "postgres"...
net user postgres >nul 2>&1 && net user postgres /delete

echo [6/8] Cancellazione delle cartelle...
call :del "%ProgramFiles%\PostgreSQL"
call :del "%ProgramFiles%\PrivacyStudio"
call :del "%LOCALAPPDATA%\PrivacyStudio"
call :del "%ProgramData%\PrivacyStudio"
call :del "%USERPROFILE%\Documents\Backup Privacy Studio"
call :del "%APPDATA%\postgresql"
call :del "%ProgramData%\Microsoft\Windows\Start Menu\Programs\Privacy Studio"
if defined BKDIR call :del "%BKDIR%"
if defined EXTDIR call :del "%EXTDIR%"
del /f /q "%PUBLIC%\Desktop\Privacy Studio.lnk" "%USERPROFILE%\Desktop\Privacy Studio.lnk" >nul 2>&1
del /f /q "%TEMP%\privacy-setup*.log" "%TEMP%\Privacy*" >nul 2>&1

echo [7/8] Pulizia del registro...
reg delete "HKLM\SOFTWARE\PostgreSQL" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\WOW6432Node\PostgreSQL" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{6F1B7C0E-4D3A-4B9E-9A52-3C7E5B1D2A90}_is1" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{6F1B7C0E-4D3A-4B9E-9A52-3C7E5B1D2A90}_is1" /f >nul 2>&1

echo [8/8] Controllo finale...
set "RESIDUI=0"
net user postgres >nul 2>&1 && (echo   RESIDUO: utente "postgres" ancora presente & set RESIDUI=1)
if exist "%ProgramFiles%\PostgreSQL" (echo   RESIDUO: %ProgramFiles%\PostgreSQL & set RESIDUI=1)
if exist "%ProgramFiles%\PrivacyStudio" (echo   RESIDUO: %ProgramFiles%\PrivacyStudio & set RESIDUI=1)
if exist "%LOCALAPPDATA%\PrivacyStudio" (echo   RESIDUO: %LOCALAPPDATA%\PrivacyStudio & set RESIDUI=1)
sc query postgresql-x64-17 >nul 2>&1 && (echo   RESIDUO: servizio postgresql-x64-17 & set RESIDUI=1)
echo.
if "%RESIDUI%"=="0" (echo PC pulito. Si puo' reinstallare. ) else (echo Restano dei residui: chiudere eventuali finestre aperte, riavviare il PC e rilanciare questo file.)
pause
exit /b 0

:del
if exist "%~1" (
  echo      cancello %~1
  takeown /f "%~1" /r /d s >nul 2>&1
  icacls "%~1" /grant *S-1-5-32-544:F /t /c >nul 2>&1
  rd /s /q "%~1" >nul 2>&1
)
exit /b 0
