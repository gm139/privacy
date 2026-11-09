# Privacy Studio — Stato dei lavori

Aggiornato: 8 novembre 2026.
Versione applicativa: 0.9.4 · Branch `main` · ultimo commit di codice: vedere `git log -1` (questo file può essere aggiornato dopo l'ultimo commit).

## Fatto (da git e documenti)

- **Documenti 01–04**: analisi normativa, specifica funzionale e dati, piano di test T01–T27, procedura di backup e ripristino.
- **Fasi A–D della specifica** realizzate in codice (`src/Privacy.Domain`, `Privacy.Data`, `Privacy.App` WPF, .NET 10; schema in `db/000–005`):
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

## Sessione del 6 novembre (dopo la prova su un PC Windows nuovo)

**Incidente:** la versione precedente di `tools/Pulisci-PC-di-prova.bat` cancellava cartelle lette da `config.json` senza controlli e ha cancellato il sistema operativo del PC di prova (nulla di importante, ma ore di ripristino). Ora il file è **uno stub che non fa nulla**. Non reintrodurre cancellazioni da percorsi variabili. Per tornare a un PC pulito si usa **Windows Sandbox** o il checkpoint di una macchina virtuale; un eventuale nuovo script solo con elenco a secco di default e percorsi fissi (vedi memoria `feedback-script-distruttivi`).

Fatto nell'installatore (`installer/PrivacyStudio.iss`), compilato ma **non provato**:
- Copia esterna **FACOLTATIVA** (decisione del titolare): campo vuoto = nessuna copia, senza domande; se indicata, si controlla che l'unità esista (avviso se è il disco di Windows). Una versione intermedia l'aveva resa obbligatoria: annullata.
- Come cartella dei backup e copia esterna sono rifiutate radici di disco, Windows, Programmi, Documenti, Desktop e profilo dell'utente.
- PostgreSQL: presente solo se esistono `bin\psql.exe` **e** il servizio `postgresql-x64-17` (né la sola chiave di registro né la sola cartella bastano: erano entrambe fonti di falsi positivi/negativi). Residui di un tentativo interrotto (cartella non vuota, servizio o utente `postgres` rimasti) e **nessun** database (`data\PG_VERSION` assente): l'installatore chiede il permesso e li rimuove da solo (servizio, cartella con takeown/icacls, chiave di registro, utente). Se trova un database, o `psql.exe` senza servizio, si ferma senza toccare nulla. Dopo l'installatore di PostgreSQL verifica il risultato (`psql.exe` + servizio), attende `pg_isready` e fallisce con i registri se non risponde; richiede almeno 3 GB liberi.
- Ultimo installatore in `C:\privacy\PrivacyStudio-Setup-0.9.4.exe` (430164214 byte, 9/11 ore 22:10, SHA-256 `9AD30F2744839F970412FF4F676E7E73E0B72A7929CD3F44A6348D30DD35B227`). Compilato da `C:\Users\mauri\progetti\Privacy-win` (copia Windows del repo, sincronizzata con rsync; SDK in `%LOCALAPPDATA%\Microsoft\dotnet-sdk8`, ISCC in `%LOCALAPPDATA%\Programs\InnoSetup`).
- 0.9.4: Prima configurazione — il «Codice fiscale o partita IVA del titolare» viene verificato (carattere di controllo del codice fiscale / cifra di controllo della partita IVA; 16 caratteri o 11 cifre). Gli errori non gestiti dell'applicazione vengono scritti in `%LOCALAPPDATA%\PrivacyStudio\errori.log` (con data/ora e tipo), così un blocco lascia una traccia leggibile.

## Da fare

1. **Scegliere come portare i file al PC di prova** (un Huawei con solo Windows, a casa, NON nella tailnet): cartella condivisa `\\lenmau\privacy` (consigliata, la condivisione la attiva il titolare), chiavetta USB, oppure Tailscale/Taildrop (non consigliato: lo collegherebbe a NAS e server). Non toccare `bardini-evo-x3` né le altre macchine.
2. **Provare l'installatore** sul PC di prova (o meglio in Windows Sandbox / VM con checkpoint; la Sandbox richiede Windows Pro e perde tutto alla chiusura: per la prova del ripristino T24 tenere il backup in una cartella condivisa dedicata). Casi da osservare: cartella `PostgreSQL\17` vuota, copia esterna vuota e indicata, prima configurazione in due passaggi, primo backup.
3. **Se l'installazione di PostgreSQL fallisce ancora**: servono `installazione.log`, `postgresql-installer-debug.log` e la cartella `logs\` in `C:\Program Files\PrivacyStudio\` (oppure `install-postgresql.log` e `bitrock_installer*.log` in `%TEMP%`). Finora la causa è stata solo ipotizzata.
4. **Se l'applicazione si blocca o si chiude**: dal PC di prova servono `C:\Users\<utente>\AppData\Local\PrivacyStudio\errori.log` (nuovo in 0.9.4, scritto dagli errori non gestiti) e, se presente, il file `Windows\Minidump\*.dmp` o l'evento di Errore applicazione nel Visualizzatore eventi.
5. Chiedere al titolare cosa non tornava nel comportamento originale della copia esterna («consigliata ma in realtà obbligatoria», poi «DEVE essere facoltativa»): il testo dell'errore non bloccante visto in installazione non è stato mai registrato.
6. Eventuale **file `.wsb`** per la Windows Sandbox (`C:\privacy` in sola lettura + cartella dedicata scrivibile per la copia esterna), da far leggere prima di aprirlo.
7. Layout di VS Code con la colonna destra divisa in tre: sistemarlo a mano (View: Reset View Locations, Editor Layout Single). Non automatizzabile.
8. Cancellare a mano la cartella di prova `C:\privacy_prova` (creata da me, contiene solo `t.bat`).

## Da fare (in ordine di priorità)

1. **Prova dell'installatore su Windows pulito** (Sandbox o macchina virtuale con checkpoint): è il blocco principale. Finora verificato solo con `/DRYRUN=1`. Seguire la lista di prova; verificare anche PostgreSQL e `pg_restore` veri (T24/T25 finora provati solo in Docker).
2. **Rapporto dei test T01–T27 con evidenze**: non esiste ancora un rapporto consolidato. Da eseguire la suite e registrare l'esito (in questa sessione non l'ho ancora lanciata: `dotnet` non è nel PATH di WSL).
3. **Procedura di aggiornamento dello schema** (migrazioni per versioni successive alla 0.9.4).
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
