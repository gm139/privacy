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

## 5. Ripristino su una macchina pulita (da provare su Windows)

Il ripristino crea sempre un database **nuovo**: se il nome esiste già, si rifiuta.

1. Installare PostgreSQL (compresi gli strumenti `pg_restore`) e l'applicazione. Indicare `pgBinDir` in `config.json`.
2. Reinstallare le chiavi (§4, «Importa chiavi…»). Senza le chiavi il file non si apre.
3. Copiare in una cartella il file `.psbk` scelto e, accanto, il suo `.ledger.jsonl` (registro delle eliminazioni).
4. Da un terminale, con l'amministratore del database nella variabile d'ambiente (non sulla riga di comando):

   ```
   set PRIVACY_ADMIN_CONN=Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=...
   Privacy.App.exe --restore "D:\Ripristino\privacy-....psbk" --database privacy [--ledger "D:\...\disposals.jsonl"]
   ```

   Il programma, in quest'ordine: verifica che il file si decifri; crea il database e i ruoli dello schema; ripristina i dati; **rielimina ciò che il backup ha riportato** usando il registro esterno; controlla la catena di audit; registra la prova di ripristino. Alla fine mostra un riepilogo: esito pieno solo se la catena di audit è integra, non ci sono eliminazioni da verificare a mano e il registro esterno è stato fornito.
5. Impostare le password dei ruoli `privacy_app` e `privacy_migrator` (non sono nel backup), puntare l'applicazione al nuovo database e controllare i dati.

Se il registro esterno non è stato fornito, i dati cancellati dopo il backup possono essere tornati visibili: il riepilogo lo segnala e **non** va ripresa l'attività normale prima di averlo recuperato ed eseguito `Privacy.App.exe --reapply-disposals`.

## 6. Limiti noti

- Backup manuale, senza pianificazione.
- Il file di backup è trattato in memoria (adatto a database di dimensioni di studio; per volumi grandi serve la cifratura a flusso).
- Il registro esterno delle eliminazioni non è cifrato né firmato: contiene solo identificativi, ma va protetto da cancellazione accidentale (è copiato con ogni backup).
- Il ripristino è provato in Docker (database di prova); la prova su Windows pulito, con PostgreSQL e `pg_restore` veri, resta da fare.
