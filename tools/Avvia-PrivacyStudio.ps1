<#
.SYNOPSIS
  Avvia Privacy Studio in ambiente di SVILUPPO su questo computer (database nel contenitore Docker di WSL).
.DESCRIPTION
  - controlla che il database sia raggiungibile su 127.0.0.1:55432 e, se non lo è, avvia il contenitore «privacy-test» in WSL;
  - compila il programma se manca;
  - imposta DOTNET_ROOT e la stringa di connessione e apre l'applicazione.
  Non è l'installazione per lo studio medico: quella userà PostgreSQL installato su Windows (v. manuale, cap. 4).
.PARAMETER Reset
  Ricrea il database dimostrativo (vuoto) e lo riempie con dati INVENTATI. Cancella i dati dimostrativi esistenti.
.PARAMETER Database
  Nome del database (predefinito: privacy_demo).
.PARAMETER NoLaunch
  Prepara tutto ma non apre il programma (utile per i controlli).
#>
param(
  [switch]$Reset,
  [string]$Database = "privacy_demo",
  [string]$WslRepo = "/home/mauri/progetti/Privacy",
  [int]$Port = 55432,
  [switch]$NoLaunch
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms

function Fail($msg) {
  [System.Windows.Forms.MessageBox]::Show($msg, "Privacy Studio (sviluppo)", "OK", "Error") | Out-Null
  Write-Error $msg; exit 1
}
function PortOpen {
  try { $c = New-Object Net.Sockets.TcpClient; $c.Connect("127.0.0.1", $Port); $c.Close(); return $true } catch { return $false }
}

$root = Split-Path -Parent $PSScriptRoot
$userSdk = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet-sdk8"
$dotnet = if (Test-Path "$userSdk\dotnet.exe") { "$userSdk\dotnet.exe" } else { "dotnet" }
if (Test-Path "$userSdk\dotnet.exe") { $env:DOTNET_ROOT = $userSdk }
$exe = Join-Path $root "src\Privacy.App\bin\Debug\net10.0-windows\Privacy.App.exe"

# 1. database
if (-not (PortOpen)) {
  Write-Host "Database non raggiungibile: provo ad avviare il contenitore in WSL..."
  $exists = (& wsl.exe docker ps -a --filter "name=^privacy-test$" --format "{{.Names}}" 2>$null) -join ""
  if ($exists -eq "privacy-test") { & wsl.exe docker start privacy-test | Out-Null }
  else { & wsl.exe bash "$WslRepo/db/start_test_db.sh" | Out-Null }
  for ($i = 0; $i -lt 30 -and -not (PortOpen); $i++) { Start-Sleep 1 }
  Start-Sleep 2
  if (-not (PortOpen)) { Fail "Il database non risponde sulla porta $Port. Controllare che Docker sia attivo in WSL." }
}

# 2. programma
if (-not (Test-Path $exe)) {
  Write-Host "Compilo il programma..."
  Push-Location $root; & $dotnet build "src\Privacy.App" -c Debug | Out-Null; Pop-Location
  if (-not (Test-Path $exe)) { Fail "Compilazione non riuscita: $exe non trovato." }
}

$env:PRIVACY_CONN = "Host=127.0.0.1;Port=$Port;Database=$Database;Username=privacy_app;Password=app"

# 3. database dimostrativo
if ($Reset) {
  Write-Host "Ricreo il database dimostrativo..."
  & wsl.exe bash "$WslRepo/db/reset_demo_db.sh" | Out-Null
  if ($LASTEXITCODE -ne 0) { Fail "Ricreazione del database non riuscita." }
  $p = Start-Process -FilePath $exe -ArgumentList "--seed-demo" -PassThru -Wait -WindowStyle Hidden
  if ($p.ExitCode -ne 0) { Fail "Caricamento dei dati dimostrativi non riuscito (codice $($p.ExitCode))." }
}

if ($NoLaunch) { Write-Host "Pronto. Database: $Database su 127.0.0.1:$Port"; exit 0 }
Write-Host "Apro Privacy Studio. Accesso dimostrativo: dott.rossi / frase di prova molto lunga 123"
Start-Process -FilePath $exe
