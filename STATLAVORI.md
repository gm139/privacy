# Privacy Studio — Stato dei lavori

Aggiornato: 6 novembre 2026 (ripresa dello sviluppo). Da aggiornare **a ogni fine sessione**.
Versione applicativa: 0.9.0 · Branch `main` · 45 commit, albero pulito all'ultimo controllo.

## Fatto (da git e documenti)

- **Documenti 01–04**: analisi normativa, specifica funzionale e dati, piano di test T01–T27, procedura di backup e ripristino.
- **Fasi A–D della specifica** realizzate in codice (`src/Privacy.Domain`, `Privacy.Data`, `Privacy.App` WPF, .NET 10; schema in `db/000–004`):
  - F01 registro dei trattamenti (revisioni, pubblicazione, esportazione alla data)
  - F02 informative e consegne (creazione e pubblicazione atomiche)
  - F03 richieste dei diritti (scadenze con `compute_due_date`)
  - F04 incidenti (conto alla rovescia 72 ore)
  - F05 conservazione ed eliminazione guidata
  - F06 backup cifrato e verificato, chiavi di recupero, ripristino su ambiente pulito (`--restore`, `--reapply-disposals`)
  - F07 rimozione di un interessato
  - audit con catena di hash, ruoli `privacy_app` / `privacy_migrator`
- Test: `tests/Privacy.Domain.Tests` e `tests/Privacy.Data.IntegrationTests` (PostgreSQL di prova via `db/tests/*.sh`).
- Interfaccia: tema chiaro/scuro/come Windows, modalità `--screenshots`, `--seed-demo`.
- **Prima configurazione in due passaggi** con password visibile (ultimi commit).
- **Manuale PDF** (60 pagine, capitolo 0 sugli adempimenti GDPR) e **lista di prova dell'installazione** (PDF), generati da `docs/manuale/`.
- **Installer Inno Setup** (`installer/PrivacyStudio.iss`) con diagnostica dell'installazione di PostgreSQL, password con «occhio», copia esterna facoltativa.

## Sessione del 6 novembre (dopo la prova su Windows pulito)

- Copia esterna **obbligatoria**: installatore (niente più casella «Non indicare», niente `/NOEXTERNAL`, rifiutato il disco di Windows) e pulsante «Esegui backup ora» (rifiuta se manca `externalBackupDir`). `BackupService` invariato (i test usano ancora `null`).
- PostgreSQL con chiave di registro ma cartella vuota o senza `bin\psql.exe` = **non installato** (viene reinstallato).
- `tools/Pulisci-PC-di-prova.bat` (copiato in `C:\privacy`): disinstalla tutto e cancella cartelle, utente `postgres`, registro.
- Installatore ricompilato da `C:\Users\mauri\progetti\Privacy-win` (copia Windows del repo, sincronizzata con rsync) e copiato in `C:\privacy`. **Non provato.**
- **Manuale e lista di prova NON aggiornati**: dicono ancora che la copia esterna è facoltativa (`part_b.py` riga 15 e 54, `prova.py` B4). Da correggere e rigenerare i PDF.

## Da fare (in ordine di priorità)

1. **Prova dell'installatore su Windows pulito** (macchina virtuale): è il blocco principale. Finora verificato solo con `/DRYRUN=1`. Seguire la lista di prova; verificare anche PostgreSQL e `pg_restore` veri (T24/T25 finora provati solo in Docker).
2. **Rapporto dei test T01–T27 con evidenze**: non esiste ancora un rapporto consolidato. Da eseguire la suite e registrare l'esito (in questa sessione non l'ho ancora lanciata: `dotnet` non è nel PATH di WSL).
3. **Procedura di aggiornamento dello schema** (migrazioni per versioni successive alla 0.9.0).
4. **Firma digitale del codice** e dell'installatore.
5. **Pianificazione automatica del backup** (oggi solo manuale); cifratura a flusso per database grandi.
6. **Protezione del registro esterno delle eliminazioni** (ora non cifrato né firmato).
7. **Prestazioni e usabilità** (documento 3, §5): dataset sintetico di alcune migliaia di interessati, misure di apertura/ricerca/stampa/backup, stampa A4, tastiera, ingrandimento; recupero chiavi eseguito da una persona non tecnica.
8. Pacchetto di consegna (documento 3, §7): note di rilascio, limiti noti, elenco decisioni aperte.

## Decisioni e conferme di altri (non si chiudono nel codice)

- **D-ENC**: scelta finale sulla protezione di anagrafica e testi liberi (v1 parte dall'opzione a: cifratura di volume/backup + ruolo ristretto). `KeyStore` e `KeyRecovery` sono soluzioni provvisorie.
- **Consulente**: regola del termine dei diritti (`deadline_rule_version = 1`, Reg. 1182/71) e approvazione dei casi attesi di T06; basi giuridiche, DPIA/DPO.
- **Titolare/medico**: configurazione effettiva (profilo, destinatari, fornitori, sistemi), approvazione informative, durate di conservazione, obiettivo di backup (24 ore proposto), ruolo privacy dello sviluppatore.
- Nessun dato reale finché non sono soddisfatte le 9 condizioni di rilascio (documento 3, §6).

## Rinviato alla v1.1

Consensi specifici (precedenza eventi, revoca per finalità, rifiuto vs assenza), più utenti e ruoli, lotti di cancellazione riprendibili, pgvector / linguaggio naturale (documento 2, §8–9; documento 3, §8).

## Note operative

- Terminologia nei testi italiani: «azienda», non «ditta».
- Ricostruire il manuale: `docs/manuale/README.md`. Ricostruire l'installatore: `installer/README.md`.
- Versioni da provare: copiarle direttamente in `C:\privacy` (WSL `/mnt/c/privacy/`): installatore, manuale, lista di prova.
