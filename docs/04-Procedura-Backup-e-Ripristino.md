# Privacy Studio — Procedura di backup, ripristino e recupero delle chiavi

Documento operativo, da provare **su una macchina pulita** prima dell'uso con dati reali (condizione di rilascio 7 del documento 3). Stato: bozza tecnica; le parti segnate «da provare» non sono ancora state eseguite su Windows.

## 1. Che cosa si salva

- **Database** (`pg_dump` formato personalizzato), cifrato con AES-256-GCM e nome file `privacy-AAAAMMGGThhmmssZ-<id>.psbk`.
- Accanto a ogni backup, un file `.json` (versione della chiave, dimensione, impronta: nessun dato personale) e una copia del **registro esterno delle eliminazioni** (`.ledger.jsonl`, solo tipo e identificativo degli elementi eliminati).
- **Non** si salva: la chiave (v. §4), la configurazione `config.json`, i documenti clinici (non sono nell'applicazione).

## 2. Configurazione (una volta)

File `%LOCALAPPDATA%\PrivacyStudio\config.json`:

```json
{ "ledgerPath": "D:\\Privacy\\disposals.jsonl",
  "backupDir": "D:\\Backup\\Privacy",
  "externalBackupDir": "E:\\CopiaEsterna",
  "pgBinDir": "C:\\Program Files\\PostgreSQL\\17\\bin" }
```

- `externalBackupDir` deve essere su un supporto **diverso** dal PC (disco esterno, altra macchina): senza, il backup non protegge da guasto o furto.
- `ledgerPath` conviene su un disco diverso da quello del database.
- Senza `pgBinDir` la scheda «Backup» lo segnala e il pulsante resta disattivato.

## 3. Backup

Scheda **Backup → Esegui backup ora**. Il programma: esegue il dump, lo cifra, lo scrive (prima su file temporaneo, poi rinomina), **lo rilegge e lo decifra dal disco** (anche dalla copia esterna) e solo allora registra l'esito «Riuscito». La scheda avvisa se l'ultimo backup verificato ha più di 24 ore (obiettivo proposto, da confermare con il medico). La pianificazione automatica non c'è ancora: per ora il backup è manuale, da fare a fine giornata.

Verifica a campione: **Backup → Verifica un file…** controlla che un file si decifri e sia una copia valida.

## 4. Chiavi di recupero (da fare subito)

Le chiavi dei backup e dei moduli sono sul PC, protette da Windows (DPAPI) e legate all'utente. **Se il profilo si perde, senza un'esportazione i backup non si aprono più.**

1. **Backup → Esporta chiavi di recupero…**, scegliere una frase segreta di almeno 14 caratteri, salvare il file `.pskr`.
2. Conservare il file **fuori dal PC** e la frase **separatamente** (es. chiavetta e busta in cassaforte).
3. Provare l'importazione su un'altra macchina prima di fidarsi: **Backup → Importa chiavi…**. Se ci sono già chiavi, l'importazione le sostituisce e chiede conferma scritta.

Questa è una soluzione provvisoria in attesa della decisione D-ENC.

## 5. Ripristino su una macchina pulita (da provare)

1. Installare PostgreSQL e l'applicazione; creare un database vuoto, ad es. `privacy`.
2. Come amministratore del database eseguire `db/000_bootstrap_roles.sql` nel database vuoto (crea i ruoli e i permessi di connessione; impostare le password dei ruoli).
3. Reinstallare le chiavi (§4, «Importa chiavi…») e copiare in una cartella il file `.psbk` scelto e il suo `.ledger.jsonl`.
4. Decifrare il backup e ripristinarlo con `pg_restore` (ruolo amministratore, database `privacy`). *Manca ancora uno strumento a riga di comando per decifrare e ripristinare in un passo: oggi la decifratura è nel codice (`BackupService.RestoreAsync`) e nei test; va esposta prima del rilascio.*
5. Copiare il `.ledger.jsonl` più recente al percorso `ledgerPath`.
6. **Eseguire `Privacy.App.exe --reapply-disposals`**: rielimina ciò che il backup ha riportato e che era già stato cancellato. Il messaggio finale riporta quanti elementi sono stati rieliminati e quanti richiedono controllo manuale (es. una richiesta ancora aperta nella copia).
7. Accedere, controllare i dati e registrare la prova (backup di riferimento, data, esito).

Finché il punto 6 non è stato eseguito, i dati cancellati dopo il backup sono di nuovo visibili: **non** riprendere l'uso normale prima.

## 6. Limiti noti

- Backup manuale, senza pianificazione.
- Il file di backup è trattato in memoria (adatto a database di dimensioni di studio; per volumi grandi serve la cifratura a flusso).
- Il registro esterno delle eliminazioni non è cifrato né firmato: contiene solo identificativi, ma va protetto da cancellazione accidentale (è copiato con ogni backup).
- Il ripristino a riga di comando (punto 4) e la prova su Windows pulito sono ancora da completare.
