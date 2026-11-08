# Programma di installazione (Inno Setup)

Bozza **non ancora provata su un Windows pulito**. Verificata solo con `/DRYRUN=1` (nessuna modifica al sistema).

Ricostruire l'installatore (da Windows, cartella `installer`):

```
dotnet publish ..\src\Privacy.App -c Release -r win-x64 --self-contained true -o publish
# copiare in redist\ postgresql-17.11-1-windows-x64.exe (EDB; SHA-256 F104C552D8495A6F20738C2A03F643164BC64B9985363329E314DEC24559F0B7)
ISCC PrivacyStudio.iss            # completo (circa 410 MB)
ISCC /DNOPG PrivacyStudio.iss     # senza PostgreSQL incorporato, per prove rapide
```

Prova a secco (per utente, senza amministratore, in una cartella temporanea):

```
PrivacyStudio-Setup-0.9.1.exe /DRYRUN=1 /PGPASSWORD=ProvaPassword-123 /EXTERNALDIR=<cartella esistente> /CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /DIR=<cartella> /LOG=<file>
```

Cosa fa l'installazione reale: installa PostgreSQL 17 (se manca) in modo non interattivo, lo limita a `localhost`, crea database `privacy` e ruoli con password casuali,
applica gli script db\000-005, scrive `connection.txt` e `config.json` nel profilo dell'utente, un file riservato agli amministratori con la password del ruolo di installazione,
crea cartelle di backup e collegamenti. La disinstallazione non cancella database, backup e chiavi.

Da fare prima del rilascio: prova su Windows pulito (anche macchina virtuale), firma digitale del codice, procedura di aggiornamento dello schema.
