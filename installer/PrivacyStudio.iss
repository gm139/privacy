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
PrivilegesRequiredOverridesAllowed=commandline
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
  BkPage: TWizardPage;
  BkEdit, ExtEdit: TNewEdit;
  BkBrowse, ExtBrowse: TNewButton;
  ShowPwd: TNewCheckBox;
  PgBase: String;
  PgExists: Boolean;
  LogFile: String;
  Failed: Boolean;
  LastCode: Integer;

function DryRun: Boolean;
begin
  Result := ExpandConstant('{param:DRYRUN|0}') = '1';
end;

function NeedPostgres: Boolean;
begin
  Result := not PgExists;
end;

{ solo il nome utente, senza dominio o nome del computer }
function ShortUser(const S: String): String;
var
  i: Integer;
begin
  Result := S;
  for i := Length(Result) downto 1 do
    if Result[i] = '\' then
    begin
      Result := Copy(Result, i + 1, Length(Result));
      exit;
    end;
end;

{ L'installazione scrive connection.txt riservato all'utente che la esegue: se non è lo stesso utente connesso
  al computer (installazione remota o come amministratore diverso), il titolare non potrebbe leggere la
  configurazione e il programma non partirebbe. Meglio fermarsi prima di creare il database. }
function InitializeSetup: Boolean;
var
  Tmp, InteractiveUser, Cmd: String;
  Buf: AnsiString;
  Code: Integer;
begin
  Result := False;
  if DryRun then begin Result := True; exit; end;
  Tmp := ExpandConstant('{tmp}\interactive-user.txt');
  DeleteFile(Tmp);
  Cmd := '-NoProfile -ExecutionPolicy Bypass -Command "(Get-CimInstance Win32_ComputerSystem).UserName | Set-Content -NoNewline -Encoding ascii ''' + Tmp + '''"';
  InteractiveUser := '';
  if Exec('powershell.exe', Cmd, '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0) then
    if LoadStringFromFile(Tmp, Buf) then InteractiveUser := Trim(String(Buf));
  DeleteFile(Tmp);
  if InteractiveUser = '' then
  begin
    MsgBox('Nessun utente è connesso al computer. L''installazione va eseguita da un utente con una sessione attiva: ' +
           'i file di configurazione sono riservati all''utente che installa.', mbError, MB_OK);
    exit;
  end;
  if CompareText(ShortUser(InteractiveUser), ShortUser(GetUserNameString)) <> 0 then
  begin
    MsgBox('L''installazione è in esecuzione come «' + GetUserNameString + '» ma al computer è connesso «' + InteractiveUser + '». ' +
           'I file di configurazione sarebbero leggibili solo da chi esegue l''installazione. ' +
           'Uscire, accedere come l''utente che userà il programma e ripetere l''installazione (l''elevazione amministrativa deve restare sullo stesso utente).',
           mbError, MB_OK);
    exit;
  end;
  Result := True;
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

{ ---------- cartelle dei backup ---------- }
function LocalDir: String;
begin
  Result := Trim(BkEdit.Text);
end;

function ExtDir: String;
begin
  Result := Trim(ExtEdit.Text);
end;

{ cartelle che non devono mai essere usate per i backup: radici di disco, cartelle di sistema e personali }
function UnsafeBackupDir(const Dir: String): Boolean;
var
  D: String;
begin
  D := RemoveBackslash(Trim(Dir));
  Result := (Length(D) < 4)
    or (CompareText(D, RemoveBackslash(ExpandConstant('{sd}'))) = 0)
    or (CompareText(D, RemoveBackslash(ExpandConstant('{win}'))) = 0)
    or (CompareText(D, RemoveBackslash(ExpandConstant('{autopf}'))) = 0)
    or (CompareText(D, RemoveBackslash(ExpandConstant('{userdocs}'))) = 0)
    or (CompareText(D, RemoveBackslash(ExpandConstant('{userdesktop}'))) = 0)
    or (CompareText(D, RemoveBackslash(ExpandConstant('{%USERPROFILE}'))) = 0)
    or (CompareText(D, RemoveBackslash(ExpandConstant('{sd}\Users'))) = 0);
end;

procedure BrowseInto(Edit: TNewEdit);
var
  D: String;
begin
  D := Edit.Text;
  if BrowseForFolder('Scegliere la cartella', D, True) then Edit.Text := D;
end;

procedure BrowseLocalClick(Sender: TObject);
begin
  BrowseInto(BkEdit);
end;

procedure BrowseExtClick(Sender: TObject);
begin
  BrowseInto(ExtEdit);
end;

function PwdCount: Integer;
begin
  if PgExists then Result := 1 else Result := 2;
end;

procedure ShowPwdClick(Sender: TObject);
var
  i: Integer;
begin
  { mostra o nasconde quanto scritto nei campi password }
  for i := 0 to PwdCount - 1 do
    TPasswordEdit(PgPage.Edits[i]).Password := not ShowPwd.Checked;
end;

procedure MakeLabel(Page: TWizardPage; const Text: String; Top: Integer);
var
  L: TNewStaticText;
begin
  L := TNewStaticText.Create(Page);
  L.Parent := Page.Surface;
  L.Caption := Text;
  L.Left := 0;
  L.Top := Top;
  L.Width := Page.SurfaceWidth;
  L.AutoSize := False;
  L.WordWrap := True;
  L.Height := ScaleY(32);
end;

{ ---------- creazione guidata ---------- }
procedure InitializeWizard;
var
  W, BtnW: Integer;
begin
  LogFile := ExpandConstant('{tmp}\privacy-setup.log');
  PgExists := RegKeyExists(HKLM64, '{#PgRegKey}');
  if PgExists then
  begin
    RegQueryStringValue(HKLM64, '{#PgRegKey}', 'Base Directory', PgBase);
    { chiave di registro rimasta ma cartella vuota o senza programmi (disinstallazione incompleta): PostgreSQL NON è installato }
    if not FileExists(PgBase + '\bin\psql.exe') then PgExists := False;
  end;
  if PgExists then
  begin
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
  { casella «Mostra le password» sotto i campi }
  ShowPwd := TNewCheckBox.Create(PgPage);
  ShowPwd.Parent := PgPage.Surface;
  ShowPwd.Caption := 'Mostra le password';
  ShowPwd.Left := 0;
  ShowPwd.Top := PgPage.Edits[PwdCount - 1].Top + PgPage.Edits[PwdCount - 1].Height + ScaleY(14);
  ShowPwd.Width := PgPage.SurfaceWidth;
  ShowPwd.Height := ScaleY(20);
  ShowPwd.OnClick := @ShowPwdClick;

  { pagina dei backup: la copia esterna è facoltativa }
  BkPage := CreateCustomPage(PgPage.ID, 'Copie di sicurezza', 'Dove salvare i backup');
  W := BkPage.SurfaceWidth;
  BtnW := ScaleX(90);
  MakeLabel(BkPage, 'Cartella dei backup sul computer:', 0);
  BkEdit := TNewEdit.Create(BkPage);
  BkEdit.Parent := BkPage.Surface;
  BkEdit.Left := 0; BkEdit.Top := ScaleY(18); BkEdit.Width := W - BtnW - ScaleX(8);
  BkEdit.Text := ExpandConstant('{userdocs}\Backup Privacy Studio');
  BkBrowse := TNewButton.Create(BkPage);
  BkBrowse.Parent := BkPage.Surface;
  BkBrowse.Caption := 'Sfoglia...'; BkBrowse.Left := W - BtnW; BkBrowse.Top := ScaleY(16); BkBrowse.Width := BtnW; BkBrowse.Height := ScaleY(24);
  BkBrowse.OnClick := @BrowseLocalClick;

  MakeLabel(BkPage, 'Copia esterna (facoltativa, ma consigliata): un supporto DIVERSO dal computer, come un disco esterno o un''altra macchina. Lasciare vuoto per non usarla; senza, il backup non protegge da guasto, furto o incendio. Si può aggiungere più tardi in config.json.', ScaleY(62));
  ExtEdit := TNewEdit.Create(BkPage);
  ExtEdit.Parent := BkPage.Surface;
  ExtEdit.Left := 0; ExtEdit.Top := ScaleY(100); ExtEdit.Width := W - BtnW - ScaleX(8);
  ExtEdit.Text := ExpandConstant('{param:EXTERNALDIR|}');
  ExtBrowse := TNewButton.Create(BkPage);
  ExtBrowse.Parent := BkPage.Surface;
  ExtBrowse.Caption := 'Sfoglia...'; ExtBrowse.Left := W - BtnW; ExtBrowse.Top := ScaleY(98); ExtBrowse.Width := BtnW; ExtBrowse.Height := ScaleY(24);
  ExtBrowse.OnClick := @BrowseExtClick;

  { installazioni non interattive (assistenza tecnica): /PGPASSWORD=... /EXTERNALDIR=... }
  PgPage.Values[0] := ExpandConstant('{param:PGPASSWORD|}');
  if not PgExists then PgPage.Values[1] := PgPage.Values[0];
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
  if CurPageID = BkPage.ID then
  begin
    if LocalDir = '' then
    begin
      MsgBox('Indicare la cartella dei backup sul computer.', mbError, MB_OK);
      Result := False;
    end
    else if UnsafeBackupDir(LocalDir) then
    begin
      MsgBox('La cartella dei backup non può essere la radice di un disco, una cartella di Windows, dei programmi o personale (Documenti, Desktop, profilo). Indicare una cartella dedicata, per esempio «Backup Privacy Studio».', mbError, MB_OK);
      Result := False;
    end
    else if (ExtDir <> '') and UnsafeBackupDir(ExtDir) then
    begin
      MsgBox('La copia esterna non può essere la radice di un disco (per esempio D:\) né una cartella di Windows, dei programmi o personale. Indicare una cartella dedicata sul disco esterno, per esempio D:\Backup Privacy Studio.', mbError, MB_OK);
      Result := False;
    end
    else if ExtDir = '' then
      Result := True
    else if (Length(ExtDir) >= 2) and (ExtDir[2] = ':') and not DirExists(Copy(ExtDir, 1, 3)) then
    begin
      MsgBox('L''unità indicata per la copia esterna non esiste o non è collegata.', mbError, MB_OK);
      Result := False;
    end
    else if CompareText(Copy(ExtDir, 1, 3), Copy(ExpandConstant('{sys}'), 1, 3)) = 0 then
      Result := MsgBox('La copia esterna è sullo stesso disco di Windows e non protegge da guasto, furto o incendio. Continuare comunque?', mbConfirmation, MB_YESNO) = IDYES;
  end;
end;

{ ---------- diagnostica ---------- }
function WindowsUserExists(const Name: String): Boolean;
var
  Code: Integer;
begin
  Result := Exec(ExpandConstant('{cmd}'), '/S /C "net user ' + Name + ' >nul 2>&1"', '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
end;

function DirNotEmpty(const Dir: String): Boolean;
var
  FR: TFindRec;
begin
  Result := False;
  if DirExists(Dir) then
    if FindFirst(Dir + '\*', FR) then
    try
      repeat
        if (FR.Name <> '.') and (FR.Name <> '..') then Result := True;
      until Result or not FindNext(FR);
    finally
      FindClose(FR);
    end;
end;

{ vero se sotto Dir c'è almeno un FILE (le cartelle vuote, anche annidate, non contano) }
function TreeHasFiles(const Dir: String): Boolean;
var
  FR: TFindRec;
begin
  Result := False;
  if not DirExists(Dir) then exit;
  if FindFirst(Dir + '\*', FR) then
  try
    repeat
      if (FR.Name <> '.') and (FR.Name <> '..') then
      begin
        if (FR.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          if TreeHasFiles(Dir + '\' + FR.Name) then Result := True;
        end
        else Result := True;
      end;
    until Result or not FindNext(FR);
  finally
    FindClose(FR);
  end;
end;

{ elimina una cartella SENZA file; se i permessi lo impediscono (residuo dell'utente «postgres» cancellato) prende possesso e riprova.
  Prima di toccare i permessi controlla che la cartella sia davvero quella di PostgreSQL. Restituisce vero se la cartella non c'è più. }
function RemoveEmptyPgDir(const Dir: String): Boolean;
var
  Code: Integer;
begin
  Result := not DirExists(Dir);
  if Result then exit;
  if TreeHasFiles(Dir) then exit;
  if (Pos('\PostgreSQL', Dir) = 0) or (Length(Dir) < 20) then exit;
  DelTree(Dir, True, True, True);
  if not DirExists(Dir) then begin Result := True; exit; end;
  Note('Cartella ' + Dir + ' non eliminabile: prendo possesso e correggo i permessi.');
  Exec(ExpandConstant('{cmd}'), '/S /C "takeown /f "' + Dir + '" /a /r /d y >nul 2>&1 <nul"', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Exec(ExpandConstant('{cmd}'), '/S /C "icacls "' + Dir + '" /grant *S-1-5-32-544:F /t /c /q >nul 2>&1 <nul"', '', SW_HIDE, ewWaitUntilTerminated, Code);
  DelTree(Dir, True, True, True);
  Result := not DirExists(Dir);
end;

{ copia nella cartella del programma i registri dell'installatore di PostgreSQL (di solito in %TEMP%) }
procedure CollectPgLogs;
var
  FR: TFindRec;
  Tmp, Dest: String;
begin
  Tmp := ExtractFilePath(RemoveBackslash(ExpandConstant('{tmp}')));
  Dest := ExpandConstant('{app}\logs');
  ForceDirectories(Dest);
  if FileExists(Tmp + 'install-postgresql.log') then CopyFile(Tmp + 'install-postgresql.log', Dest + '\install-postgresql.log', False);
  if FindFirst(Tmp + 'bitrock_installer*.log', FR) then
  try
    repeat
      CopyFile(Tmp + FR.Name, Dest + '\' + FR.Name, False);
    until not FindNext(FR);
  finally
    FindClose(FR);
  end;
  Note('Registri di PostgreSQL copiati in ' + Dest);
end;

{ ---------- esecuzione ---------- }
function RunStep(const Desc, FileName, Params: String): Boolean;
var
  Code: Integer;
begin
  Note('> ' + Desc);
  if DryRun then begin Result := True; exit; end;
  Result := Exec(FileName, Params, '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
  LastCode := Code;
  if not Result then Note('  ERRORE: codice ' + IntToStr(Code));
end;

{ psql legge la password da un file temporaneo pgpass (PGPASSFILE): non compare né sulla riga di comando
  né nella lista dei processi, e i caratteri speciali della password non vanno quotati per cmd.
  Il file è scritto nella cartella temporanea e cancellato subito dopo l'esecuzione. }
function Psql(const Desc, User, Pwd, Db, Args: String): Boolean;
var
  PassFile: String;
begin
  if DryRun then begin Result := RunStep(Desc, '', ''); exit; end;
  PassFile := ExpandConstant('{tmp}\pgpass.tmp');
  if not SaveStringToFile('127.0.0.1:5432:' + Db + ':' + User + ':' + Pwd + #13#10, PassFile, False) then
  begin
    Note('  ERRORE: impossibile creare il file temporaneo delle credenziali');
    Result := False;
    exit;
  end;
  try
    Result := RunStep(Desc, ExpandConstant('{cmd}'),
      '/S /C "set PGPASSFILE=' + PassFile + '&& "' + PgBase + '\bin\psql.exe" -h 127.0.0.1 -p 5432 -U ' + User + ' -d ' + Db + ' -v ON_ERROR_STOP=1 -q ' + Args + '"');
  finally
    DeleteFile(PassFile);
  end;
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
  SuperPwd, AppPwd, MigPwd, ConnFile, ConfFile, Conf, SecretDir, Cs: String;
  i: Integer;
  Scripts: array[0..4] of String;
  Existing: Boolean;
begin
  if DryRun then Note('Privacy Studio - installazione (PROVA A SECCO: nessuna modifica)')
  else Note('Privacy Studio - installazione ' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':'));
  SuperPwd := PgPage.Values[0];
  Scripts[0] := '001_schema_v1.sql'; Scripts[1] := '002_catalogs.sql'; Scripts[2] := '003_rules_and_grants.sql'; Scripts[3] := '004_disposal_ledger.sql';
  Scripts[4] := '005_hardening.sql';
  ConnFile := ExpandConstant('{localappdata}\PrivacyStudio\connection.txt');
  ConfFile := ExpandConstant('{localappdata}\PrivacyStudio\config.json');

  { 1. PostgreSQL }
  if not PgExists then
  begin
    { residui di un'installazione precedente rimasta a metà: fanno fallire l'installatore di PostgreSQL }
    if not DryRun then
    begin
      if WindowsUserExists('postgres') then
      begin
        Note('Esiste già un utente Windows «postgres» ma PostgreSQL 17 non risulta installato.');
        MsgBox('Sul computer esiste già un utente Windows chiamato «postgres», ma PostgreSQL 17 non risulta installato: di solito è il residuo di un''installazione interrotta. ' +
               'L''installatore di PostgreSQL in questo caso fallisce.' + #13#10 + #13#10 +
               'Da un prompt dei comandi come amministratore: net user postgres /delete' + #13#10 +
               'poi eliminare la cartella ' + ExpandConstant('{autopf}') + '\PostgreSQL (se esiste) e rieseguire questo programma di installazione.', mbError, MB_OK);
        Failed := True; exit;
      end;
      { cartella di PostgreSQL presente ma senza file (residuo di una disinstallazione): va tolta, altrimenti l'installatore di PostgreSQL fallisce }
      if DirExists(ExpandConstant('{autopf}\PostgreSQL\17')) and not TreeHasFiles(ExpandConstant('{autopf}\PostgreSQL\17')) then
      begin
        if RemoveEmptyPgDir(ExpandConstant('{autopf}\PostgreSQL\17')) then
          Note('Cartella PostgreSQL 17 vuota: eliminata.')
        else
        begin
          Note('Cartella PostgreSQL 17 vuota ma non eliminabile.');
          MsgBox('La cartella ' + ExpandConstant('{autopf}\PostgreSQL\17') + ' è vuota ma non riesco a eliminarla (è aperta in Esplora file o in un altro programma?). ' +
                 'Chiuderla, poi riavviare il computer e rieseguire questo programma di installazione.', mbError, MB_OK);
          Failed := True; exit;
        end;
        if DirExists(ExpandConstant('{autopf}\PostgreSQL')) and not TreeHasFiles(ExpandConstant('{autopf}\PostgreSQL')) then
          RemoveEmptyPgDir(ExpandConstant('{autopf}\PostgreSQL'));
      end;
      { chiave di registro rimasta senza programmi: va tolta, altrimenti l'installatore di PostgreSQL la scambia per un'installazione esistente }
      if RegKeyExists(HKLM64, '{#PgRegKey}') then
      begin
        Note('Chiave di registro di PostgreSQL senza programmi: eliminata.');
        RegDeleteKeyIncludingSubkeys(HKLM64, '{#PgRegKey}');
      end;
      if TreeHasFiles(ExpandConstant('{autopf}\PostgreSQL\17')) then
      begin
        Note('La cartella ' + ExpandConstant('{autopf}\PostgreSQL\17') + ' esiste già e non è vuota.');
        MsgBox('La cartella ' + ExpandConstant('{autopf}\PostgreSQL\17') + ' esiste già e non è vuota, ma PostgreSQL 17 non risulta installato. ' +
               'Probabile residuo di un''installazione interrotta: eliminarla (dopo aver controllato che non contenga dati da conservare) e rieseguire.', mbError, MB_OK);
        Failed := True; exit;
      end;
    end;
    if not RunStep('Installazione di PostgreSQL 17 (qualche minuto)', ExpandConstant('{tmp}\postgresql-17.11-1-windows-x64.exe'),
      '--mode unattended --unattendedmodeui none --superpassword "' + SuperPwd + '" --servicename {#PgServiceName} --serverport 5432 ' +
      '--enable-components server,commandlinetools --disable-components pgAdmin,stackbuilder ' +
      '--debuglevel 4 --debugtrace "' + ExpandConstant('{app}') + '\postgresql-installer-debug.log"') then
    begin
      Note('PostgreSQL: l''installatore ha restituito il codice ' + IntToStr(LastCode));
      if not DryRun then CollectPgLogs;
      Failed := True; exit;
    end;
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
  for i := 0 to 4 do
    if not Psql('Schema: ' + Scripts[i], 'privacy_migrator', MigPwd, 'privacy', '-f "' + ExpandConstant('{app}') + '\db\' + Scripts[i] + '"') then begin Failed := True; exit; end;

  { 6. configurazione dell'utente }
  if not DryRun then
  begin
    ForceDirectories(ExtractFileDir(ConnFile));
    Cs := 'Host=127.0.0.1;Port=5432;Database=privacy;Username=privacy_app;Password=' + AppPwd;
    SaveStringToFile(ConnFile, Cs, False);
    Conf := '{ "ledgerPath": "' + JsonEsc(LocalDir + '\disposals.jsonl') + '",' + #13#10 +
            '  "backupDir": "' + JsonEsc(LocalDir) + '",' + #13#10;
    if ExtDir <> '' then Conf := Conf + '  "externalBackupDir": "' + JsonEsc(ExtDir) + '",' + #13#10;
    Conf := Conf + '  "pgBinDir": "' + JsonEsc(PgBase + '\bin') + '" }';
    SaveStringToFile(ConfFile, Conf, False);
    ForceDirectories(LocalDir);
    if ExtDir <> '' then ForceDirectories(ExtDir);
    { password del ruolo di installazione (serve per gli aggiornamenti dello schema): file riservato agli amministratori }
    SecretDir := ExpandConstant('{commonappdata}\PrivacyStudio');
    ForceDirectories(SecretDir);
    SaveStringToFile(SecretDir + '\migrator.secret', MigPwd, False);
    { se i permessi non si chiudono l'installazione si ferma: un file con una password leggibile da tutti è peggio di un'installazione incompleta }
    if not RunStep('Protezione del file riservato', ExpandConstant('{cmd}'),
      '/S /C "icacls "' + SecretDir + '\migrator.secret" /inheritance:r /grant:r *S-1-5-32-544:F *S-1-5-18:F"') then begin Failed := True; exit; end;
    { il file di connessione contiene la password del ruolo operativo: solo l''utente che installa, gli amministratori e il sistema }
    if not RunStep('Protezione del file di connessione', ExpandConstant('{cmd}'),
      '/S /C "icacls "' + ConnFile + '" /inheritance:r /grant:r "' + GetUserNameString + ':F" *S-1-5-32-544:F *S-1-5-18:F"') then begin Failed := True; exit; end;
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
    if DryRun then CopyFile(LogFile, ExpandConstant('{app}\dryrun.log'), False)
    else CopyFile(LogFile, ExpandConstant('{app}\installazione.log'), False);
    if Failed then
      MsgBox('L''installazione del database non è andata a buon fine (ultimo codice di errore: ' + IntToStr(LastCode) + '). Il programma è stato copiato ma non è utilizzabile.' + #13#10 +
             'Il dettaglio è in: ' + ExpandConstant('{app}') + '\installazione.log' + #13#10 +
             'Registri di PostgreSQL (se disponibili) in: ' + ExpandConstant('{app}') + '\logs' + #13#10 + 'Inviarli all''assistenza.', mbError, MB_OK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    MsgBox('Privacy Studio è stato rimosso. Il database, le copie di sicurezza e le chiavi NON sono stati eliminati: contengono dati dello studio e vanno conservati o cancellati con decisione consapevole del titolare.', mbInformation, MB_OK);
end;
