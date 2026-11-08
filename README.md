# Privacy Studio

Applicazione desktop Windows per gestire gli adempimenti privacy/GDPR di uno **studio medico**: registro dei trattamenti, informative e consegne, richieste degli interessati, violazioni dei dati, conservazione ed eliminazione, backup cifrato e ripristino.

Il programma documenta attività e scadenze e aiuta a dimostrare il lavoro fatto. **Non è una consulenza legale e non certifica la conformità** al Regolamento (UE) 2016/679: le valutazioni restano in capo al titolare del trattamento e al suo consulente.

> **Stato: 0.9.3 — pre-rilascio.**
> Non usare con dati reali dei pazienti. L'applicazione è stata provata con dati sintetici e test automatici, non ancora con dati reali né su un Windows pulito. L'installer non è firmato. Le condizioni da soddisfare prima dell'uso con dati reali sono elencate in [`docs/03-Piano-di-Test-e-Rilascio.md`](docs/03-Piano-di-Test-e-Rilascio.md) (§6).

## Funzioni

- **F01 — Registro dei trattamenti**: finalità, basi giuridiche, destinatari, revisioni, pubblicazione ed esportazione alla data.
- **F02 — Informative e consegne**: creazione e pubblicazione atomica dei testi, registrazione delle consegne al paziente e storico.
- **F03 — Richieste degli interessati**: accesso, rettifica, cancellazione, limitazione, portabilità, opposizione; calcolo delle scadenze.
- **F04 — Violazioni dei dati**: registro degli incidenti e conto alla rovescia delle 72 ore.
- **F05 — Conservazione ed eliminazione**: regole di conservazione, eliminazione guidata e registro esterno delle eliminazioni.
- **F06 — Backup e ripristino**: backup cifrati e verificati (`.psbk`), chiavi di recupero (`.pskr`), ripristino su ambiente pulito.
- **F07 — Rimozione di un interessato**: procedura tracciata.
- **Audit**: catena di hash sulle operazioni; ruoli database separati `privacy_app` (operativo) e `privacy_migrator` (migrazioni).
- **Interfaccia**: WPF con tema chiaro/scuro/come Windows, prima configurazione in due passaggi, modalità `--screenshots` e `--seed-demo`.

## Architettura

| Percorso | Contenuto |
|---|---|
| `src/Privacy.Domain` | Logica di dominio pura: scadenze, incidenti, password, chiavi di recupero, template delle informative. |
| `src/Privacy.Data` | Accesso a PostgreSQL, servizi applicativi, backup/ripristino, audit. |
| `src/Privacy.App` | Applicazione desktop WPF (`net10.0-windows`). |
| `db/` | Schema PostgreSQL `000`–`004`, cataloghi, regole/permessi, test SQL e script per il database di prova. |
| `tests/` | Test unitari (`net8.0`) e test di integrazione su PostgreSQL. |
| `installer/` | Script Inno Setup e istruzioni per costruire l'installer. |
| `docs/` | Analisi normativa, specifica funzionale e dati, piano di test, procedura di backup, manuale PDF e lista di prova. |
| `tools/` | Script di avvio. `Pulisci-PC-di-prova.bat` è volutamente disattivato: non eseguiva controlli sui percorsi e poteva cancellare cartelle non volute. |

Il database è PostgreSQL 17, in ascolto solo su `127.0.0.1`. L'installer crea database e ruoli, applica le migrazioni e scrive la configurazione locale in `%LOCALAPPDATA%\PrivacyStudio`.

## Requisiti

- Windows 10/11 a 64 bit.
- [.NET SDK 10](https://dotnet.microsoft.com/download) per compilare l'applicazione.
- .NET 8 SDK o runtime 8 per i test, che hanno come target `net8.0`.
- PostgreSQL 17 per l'uso locale (l'installer lo include; per lo sviluppo va bene un'istanza locale o Docker).
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) solo per costruire l'installer.
- Docker solo per i test SQL e per il database di prova.

## Compilazione

Da Windows, nella radice del repository:

```powershell
dotnet restore Privacy.sln
dotnet build Privacy.sln -c Release
```

Pubblicazione self-contained dell'applicazione:

```powershell
dotnet publish src\Privacy.App -c Release -r win-x64 --self-contained true -o publish
```

## Avvio in sviluppo

Il programma legge la stringa di connessione dalla variabile `PRIVACY_CONN` oppure da `%LOCALAPPDATA%\PrivacyStudio\connection.txt`.

1. Avviare il PostgreSQL di prova (richiede Docker; ascolta solo su `127.0.0.1:55432`):

   ```bash
   db/start_test_db.sh
   ```

2. Impostare la connessione e avviare l'applicazione:

   ```powershell
   $env:PRIVACY_CONN = "Host=127.0.0.1;Port=55432;Database=privacy;Username=privacy_app;Password=app"
   dotnet run --project src\Privacy.App
   ```

Al primo avvio compare la procedura di prima configurazione (dati dello studio, utente e password). Per fermare il database di prova: `db/start_test_db.sh stop`.

## Test

Test unitari:

```powershell
dotnet test tests\Privacy.Domain.Tests
```

Test di integrazione (prima avviare `db/start_test_db.sh`):

```powershell
$env:PRIVACY_TEST_CONN = "Host=127.0.0.1;Port=55432;Database=privacy;Username=privacy_app;Password=app"
dotnet test tests\Privacy.Data.IntegrationTests
```

Test dello schema SQL in un container dedicato e usa-e-getta:

```bash
db/run_tests.sh
```

## Installer

L'installer completo incorpora PostgreSQL 17 e pesa circa 410 MB. Le istruzioni per ricostruirlo (incluso il download del pacchetto EDB e il calcolo dell'impronta) sono in [`installer/README.md`](installer/README.md).

```powershell
cd installer
dotnet publish ..\src\Privacy.App -c Release -r win-x64 --self-contained true -o publish
ISCC PrivacyStudio.iss
```

Gli asset precompilati sono nella pagina [Releases](../../releases). La versione `v0.9.3` include installer, manuale PDF e lista di prova dell'installazione.

## Documentazione

- [Analisi normativa per lo studio medico](docs/01-Analisi-Normativa-Studio-Medico.md)
- [Specifica funzionale e dati](docs/02-Specifica-Funzionale-e-Dati.md)
- [Piano di test e condizioni di rilascio](docs/03-Piano-di-Test-e-Rilascio.md)
- [Procedura di backup e ripristino](docs/04-Procedura-Backup-e-Ripristino.md)
- [Manuale d'uso e di conformità (PDF)](docs/Manuale-Privacy-Studio.pdf)
- [Lista di prova dell'installazione (PDF)](docs/Lista-di-prova-installazione.pdf)
- [Come ricostruire il manuale](docs/manuale/README.md)

## Stato e limiti noti

- Versione **0.9.3**, da considerare una bozza: mancano la prova dell'installazione su un Windows pulito, il rapporto consolidato dei test T01–T27, la firma digitale di codice e installer e la procedura di aggiornamento dello schema.
- Il backup è manuale; non è ancora pianificato automaticamente.
- Il registro esterno delle eliminazioni non è cifrato né firmato.
- La scelta definitiva sulla cifratura di anagrafica e testi liberi (decisione D-ENC) è ancora aperta.
- Il file `tools/Pulisci-PC-di-prova.bat` è uno stub che non esegue operazioni: non reintrodurre cancellazioni basate su percorsi letti da file di configurazione.

Per le decisioni aperte e le condizioni di rilascio si veda [`docs/03-Piano-di-Test-e-Rilascio.md`](docs/03-Piano-di-Test-e-Rilascio.md) e [`STATLAVORI.md`](STATLAVORI.md).
