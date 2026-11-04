; PrivacyStudio.iss - programma di installazione di Privacy Studio (Inno Setup 6)
;
; STATO: bozza NON ancora provata su un computer Windows pulito. Il flusso è stato verificato solo con /DRYRUN=1 (nessuna modifica al sistema).
;
; Compilazione (dalla cartella installer):
;   1. dotnet publish ..\src\Privacy.App -c Release -r win-x64 --self-contained true -o publish
;   2. copiare in redist\ il programma di installazione di PostgreSQL 17 (EDB), nome indicato in PgInstaller
;   3. ISCC PrivacyStudio.iss            (installatore completo, circa 0,5 GB)
;      ISCC /DNOPG PrivacyStudio.iss     (senza PostgreSQL incorporato: solo per prove rapide)
; Prova senza toccare il sistema:  PrivacyStudio-Setup-0.9.0.exe /DRYRUN=1 /VERYSILENT /SUPPRESSMSGBOXES

#define AppName "Privacy Studio"
#define AppVersion "0.9.0"
#define PgInstaller "redist\postgresql-17.11-1-windows-x64.exe"
#define PgServiceName "postgresql-x64-17"
#define PgRegKey "SOFTWARE\PostgreSQL\Installations\postgresql-x64-17"

[Setup]
AppId={{6F1B7C0E-4D3A-4B9E-9A52-3C7E5B1D2A90}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Studio medico
DefaultDirName={autopf}\PrivacyStudio
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=PrivacyStudio-Setup-{#AppVersion}
Compression=lzma2/fast
SolidCompression=no
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\Privacy.App.exe
MinVersion=10.0

[Languages]
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"

[Tasks]
Name: "desktopicon"; Description: "Crea un collegamento sul desktop"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
Source: "..\db\*.sql"; DestDir: "{app}\db"; Flags: ignoreversion
Source: "..\docs\Manuale-Privacy-Studio.pdf"; DestDir: "{app}\docs"; Flags: ignoreversion
#ifndef NOPG
Source: "{#PgInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedPostgres
#endif

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\Privacy.App.exe"
Name: "{group}\Manuale d'uso"; Filename: "{app}\docs\Manuale-Privacy-Studio.pdf"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Privacy.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Privacy.App.exe"; Description: "Avvia {#AppName}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
var
  PgPage: TInputQueryWizardPage;
  BackupPage: TInputDirWizardPage;
  PgBase: String;
  PgExists: Boolean;
  LogFile: String;
  Failed: Boolean;

function DryRun: Boolean;
begin
  Result := ExpandConstant('{param:DRYRUN|0}') = '1';
end;

function NeedPostgres: Boolean;
begin
  Result := not PgExists;
end;

procedure Note(const S: String);
begin
  Log(S);
  SaveStringToFile(LogFile, S + #13#10, True);
end;

{ ---------- validazione ---------- }
function PwdChars(const S: String; const Allowed: String): Boolean;
var
  i: Integer;
  c: Char;
begin
  Result := True;
  for i := 1 to Length(S) do
  begin
    c := S[i];
    if not (((c >= 'a') and (c <= 'z')) or ((c >= 'A') and (c <= 'Z')) or ((c >= '0') and (c <= '9')) or (Pos(c, Allowed) > 0)) then
      Result := False;
  end;
end;

function NoShellChars(const S: String): Boolean;
var
  i: Integer;
begin
  Result := True;
  for i := 1 to Length(S) do
    if Pos(S[i], '"%^&|<>!`''') > 0 then Result := False;
end;

{ ---------- creazione guidata ---------- }
procedure InitializeWizard;
begin
  LogFile := ExpandConstant('{tmp}\privacy-setup.log');
  PgExists := RegKeyExists(HKLM64, '{#PgRegKey}') or DryRun and False;
  if PgExists then
  begin
    RegQueryStringValue(HKLM64, '{#PgRegKey}', 'Base Directory', PgBase);
    PgPage := CreateInputQueryPage(wpSelectDir, 'Database', 'PostgreSQL è già installato',
      'Inserire la password dell''utente amministratore (postgres) del PostgreSQL 17 già presente. Serve solo all''installazione per creare il database dello studio e non viene salvata.');
    PgPage.Add('Password dell''utente postgres:', True);
  end
  else
  begin
    PgBase := ExpandConstant('{autopf}\PostgreSQL\17');
    PgPage := CreateInputQueryPage(wpSelectDir, 'Database', 'Password dell''amministratore del database',
      'Verrà installato PostgreSQL 17. Scegliere la password dell''utente amministratore (postgres). Annotarla in un luogo sicuro: serve per il ripristino dei backup e per la manutenzione. ' +
      'Almeno 10 caratteri; sono ammessi lettere, cifre e i simboli - _ . @ # + =');
    PgPage.Add('Password:', True);
    PgPage.Add('Ripetere la password:', True);
  end;
  BackupPage := CreateInputDirPage(PgPage.ID, 'Copie di sicurezza', 'Dove salvare i backup',
    'La copia esterna deve trovarsi su un supporto DIVERSO dal computer (disco esterno o altra macchina): senza, il backup non protegge da guasto, furto o incendio. Si può cambiare più tardi in config.json.',
    False, '');
  BackupPage.Add('Cartella dei backup sul computer:');
  BackupPage.Add('Copia esterna (consigliata):');
  BackupPage.Values[0] := ExpandConstant('{userdocs}\Backup Privacy Studio');
  BackupPage.Values[1] := '';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = PgPage.ID then
  begin
    if PgExists then
    begin
      if (PgPage.Values[0] = '') or not NoShellChars(PgPage.Values[0]) then
      begin
        MsgBox('Inserire la password. Non sono ammessi i caratteri  " % ^ & | < > ! ` ''', mbError, MB_OK);
        Result := False;
      end;
    end
    else
    begin
      if (Length(PgPage.Values[0]) < 10) or not PwdChars(PgPage.Values[0], '-_.@#+=') then
      begin
        MsgBox('La password deve avere almeno 10 caratteri e contenere solo lettere, cifre e i simboli - _ . @ # + =', mbError, MB_OK);
        Result := False;
      end
      else if PgPage.Values[0] <> PgPage.Values[1] then
      begin
        MsgBox('Le due password non coincidono.', mbError, MB_OK);
        Result := False;
      end;
    end;
  end;
  if CurPageID = BackupPage.ID then
  begin
    if Trim(BackupPage.Values[0]) = '' then
    begin
      MsgBox('Indicare la cartella dei backup.', mbError, MB_OK);
      Result := False;
    end
    else if Trim(BackupPage.Values[1]) = '' then
      Result := MsgBox('Non è stata indicata una copia esterna. Senza, i backup restano sullo stesso computer e non proteggono da guasto o furto. Continuare comunque?', mbConfirmation, MB_YESNO) = IDYES
    else if CompareText(Copy(BackupPage.Values[1], 1, 3), Copy(ExpandConstant('{sys}'), 1, 3)) = 0 then
      Result := MsgBox('La copia esterna è sullo stesso disco di Windows. Continuare comunque?', mbConfirmation, MB_YESNO) = IDYES;
  end;
end;

{ ---------- esecuzione ---------- }
function RunStep(const Desc, FileName, Params: String): Boolean;
var
  Code: Integer;
begin
  Note('> ' + Desc);
  if DryRun then begin Result := True; exit; end;
  Result := Exec(FileName, Params, '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
  if not Result then Note('  ERRORE: codice ' + IntToStr(Code));
end;

{ psql con la password nell'ambiente del processo figlio (mai sulla riga di comando di psql); la descrizione non contiene mai password }
function Psql(const Desc, User, Pwd, Db, Args: String): Boolean;
begin
  Result := RunStep(Desc, ExpandConstant('{cmd}'),
    '/S /C "set PGPASSWORD=' + Pwd + '&& "' + PgBase + '\bin\psql.exe" -h 127.0.0.1 -p 5432 -U ' + User + ' -d ' + Db + ' -v ON_ERROR_STOP=1 -q ' + Args + '"');
end;

function SecureRandom(N: Integer): String;
var
  Tmp, Cmd: String;
  Code: Integer;
  Buf: AnsiString;
begin
  if DryRun then begin Result := 'PasswordDiProvaDryRun0123'; exit; end;
  Tmp := ExpandConstant('{tmp}\rnd.txt');
  Cmd := '-NoProfile -ExecutionPolicy Bypass -Command "$b=[byte[]]::new(64);[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b);' +
         '([Convert]::ToBase64String($b) -replace ''[^A-Za-z0-9]'','''').Substring(0,' + IntToStr(N) + ') | Set-Content -NoNewline -Encoding ascii ''' + Tmp + '''"';
  Result := '';
  if Exec('powershell.exe', Cmd, '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0) then
    if LoadStringFromFile(Tmp, Buf) then Result := Trim(String(Buf));
  DeleteFile(Tmp);
end;

function JsonEsc(const S: String): String;
begin
  Result := S;
  StringChangeEx(Result, '\', '\\', True);
end;

function ScalarOf(const Sql, Pwd: String): String;
var
  Tmp: String;
  Buf: AnsiString;
begin
  Result := '';
  if DryRun then exit;
  Tmp := ExpandConstant('{tmp}\scalar.txt');
  DeleteFile(Tmp);
  Psql('interrogazione', 'postgres', Pwd, 'postgres', '-tAc "' + Sql + '" > "' + Tmp + '"');
  if LoadStringFromFile(Tmp, Buf) then Result := Trim(String(Buf));
end;

procedure DoInstall;
var
  SuperPwd, AppPwd, MigPwd, ConnFile, ConfFile, Conf, SecretDir, Cs, Dir: String;
  i: Integer;
  Scripts: array[0..3] of String;
  Existing: Boolean;
begin
  Note('Privacy Studio - installazione ' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':') + IfThen(DryRun, ' (PROVA A SECCO: nessuna modifica)', ''));
  SuperPwd := PgPage.Values[0];
  Scripts[0] := '001_schema_v1.sql'; Scripts[1] := '002_catalogs.sql'; Scripts[2] := '003_rules_and_grants.sql'; Scripts[3] := '004_disposal_ledger.sql';
  ConnFile := ExpandConstant('{localappdata}\PrivacyStudio\connection.txt');
  ConfFile := ExpandConstant('{localappdata}\PrivacyStudio\config.json');

  { 1. PostgreSQL }
  if not PgExists then
  begin
    if not RunStep('Installazione di PostgreSQL 17 (qualche minuto)', ExpandConstant('{tmp}\postgresql-17.11-1-windows-x64.exe'),
      '--mode unattended --unattendedmodeui none --superpassword "' + SuperPwd + '" --servicename {#PgServiceName} --serverport 5432 ' +
      '--enable-components server,commandlinetools --disable-components pgAdmin,stackbuilder') then
    begin Failed := True; exit; end;
    if not DryRun then RegQueryStringValue(HKLM64, '{#PgRegKey}', 'Base Directory', PgBase);
  end;
  Note('PostgreSQL in: ' + PgBase);

  { 2. attesa del servizio }
  if not DryRun then
  begin
    for i := 1 to 60 do
    begin
      if RunStep('Attesa del database', PgBase + '\bin\pg_isready.exe', '-h 127.0.0.1 -p 5432') then break;
      Sleep(1000);
    end;
  end;

  { 3. il database accetta solo connessioni dal computer stesso }
  if Psql('Impostazione: database in ascolto solo su questo computer', 'postgres', SuperPwd, 'postgres', '-c "ALTER SYSTEM SET listen_addresses = ''localhost''"') then
    RunStep('Riavvio del servizio database', ExpandConstant('{cmd}'), '/S /C "net stop {#PgServiceName} && net start {#PgServiceName}"')
  else begin Failed := True; exit; end;

  { 4. database già presente? (aggiornamento del programma: i dati restano) }
  Existing := ScalarOf('select 1 from pg_database where datname = ''privacy''', SuperPwd) = '1';
  if Existing and FileExists(ConnFile) then
  begin
    Note('Database e configurazione già presenti: i dati NON vengono toccati. Lo schema non viene aggiornato (procedura di aggiornamento da definire).');
    exit;
  end;
  if Existing then
  begin
    MsgBox('Esiste già un database «privacy» ma manca il file di connessione di questo utente. Per non rischiare di sovrascrivere dati, l''installazione si ferma: contattare l''assistenza.', mbError, MB_OK);
    Failed := True; exit;
  end;

  { 5. ruoli e schema }
  AppPwd := SecureRandom(24); MigPwd := SecureRandom(24);
  if (AppPwd = '') or (MigPwd = '') then begin Note('Generazione password non riuscita'); Failed := True; exit; end;
  if not Psql('Creazione del database', 'postgres', SuperPwd, 'postgres', '-c "CREATE DATABASE privacy"') then begin Failed := True; exit; end;
  if not Psql('Creazione dei ruoli', 'postgres', SuperPwd, 'privacy', '-f "' + ExpandConstant('{app}') + '\db\000_bootstrap_roles.sql"') then begin Failed := True; exit; end;
  if not Psql('Password del ruolo operativo', 'postgres', SuperPwd, 'privacy', '-c "ALTER ROLE privacy_app PASSWORD ''' + AppPwd + '''"') then begin Failed := True; exit; end;
  if not Psql('Password del ruolo di installazione', 'postgres', SuperPwd, 'privacy', '-c "ALTER ROLE privacy_migrator PASSWORD ''' + MigPwd + '''"') then begin Failed := True; exit; end;
  for i := 0 to 3 do
    if not Psql('Schema: ' + Scripts[i], 'privacy_migrator', MigPwd, 'privacy', '-f "' + ExpandConstant('{app}') + '\db\' + Scripts[i] + '"') then begin Failed := True; exit; end;

  { 6. configurazione dell'utente }
  if not DryRun then
  begin
    ForceDirectories(ExtractFileDir(ConnFile));
    Cs := 'Host=127.0.0.1;Port=5432;Database=privacy;Username=privacy_app;Password=' + AppPwd;
    SaveStringToFile(ConnFile, Cs, False);
    Conf := '{ "ledgerPath": "' + JsonEsc(BackupPage.Values[0] + '\disposals.jsonl') + '",' + #13#10 +
            '  "backupDir": "' + JsonEsc(BackupPage.Values[0]) + '",' + #13#10;
    if Trim(BackupPage.Values[1]) <> '' then Conf := Conf + '  "externalBackupDir": "' + JsonEsc(BackupPage.Values[1]) + '",' + #13#10;
    Conf := Conf + '  "pgBinDir": "' + JsonEsc(PgBase + '\bin') + '" }';
    SaveStringToFile(ConfFile, Conf, False);
    ForceDirectories(BackupPage.Values[0]);
    if Trim(BackupPage.Values[1]) <> '' then ForceDirectories(BackupPage.Values[1]);
    { password del ruolo di installazione (serve per gli aggiornamenti dello schema): file riservato agli amministratori }
    SecretDir := ExpandConstant('{commonappdata}\PrivacyStudio');
    ForceDirectories(SecretDir);
    SaveStringToFile(SecretDir + '\migrator.secret', MigPwd, False);
    RunStep('Protezione del file riservato', ExpandConstant('{cmd}'),
      '/S /C "icacls "' + SecretDir + '\migrator.secret" /inheritance:r /grant:r *S-1-5-32-544:F *S-1-5-18:F"');
    { il file di connessione contiene la password del ruolo operativo: solo l''utente che installa, gli amministratori e il sistema }
    RunStep('Protezione del file di connessione', ExpandConstant('{cmd}'),
      '/S /C "icacls "' + ConnFile + '" /inheritance:r /grant:r "' + GetUserNameString + ':F" *S-1-5-32-544:F *S-1-5-18:F"');
  end
  else Note('> Scrittura di connection.txt, config.json e del file riservato (non eseguita in prova a secco)');
  Note('Fatto.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    DoInstall;
    if DryRun then SaveStringToFile(ExpandConstant('{app}\dryrun.log'), '', True);
    if DryRun then FileCopy(LogFile, ExpandConstant('{app}\dryrun.log'), False)
    else FileCopy(LogFile, ExpandConstant('{app}\installazione.log'), False);
    if Failed then
      MsgBox('L''installazione del database non è andata a buon fine. Il programma è stato copiato ma non è utilizzabile.' + #13#10 +
             'Il dettaglio è in: ' + ExpandConstant('{app}') + '\installazione.log' + #13#10 + 'Contattare l''assistenza.', mbError, MB_OK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    MsgBox('Privacy Studio è stato rimosso. Il database, le copie di sicurezza e le chiavi NON sono stati eliminati: contengono dati dello studio e vanno conservati o cancellati con decisione consapevole del titolare.', mbInformation, MB_OK);
end;
