# Privacy Studio — Piano di test e condizioni di rilascio

Data: 4 novembre 2026. Versione 2.0. Documento 3 di 3. Riferimenti: *01 Analisi normativa*, *02 Specifica funzionale e dati* (§ e F01–F07 si riferiscono a quest'ultimo).

I test usano **dati sintetici**. Esistono tre suite eseguibili:

- `db/run_tests.sh`: 132 controlli sullo schema (T01–T22 lato database, termini, audit, privilegi), su un PostgreSQL temporaneo.
- `dotnet test` (soluzione `Privacy.sln`): test del dominio e di integrazione sui flussi F01–F07, compresa la parità fra il calcolo dei termini in C# e in SQL (ogni giorno 2026–2030). Per i test di integrazione serve `db/start_test_db.sh` e la variabile `PRIVACY_TEST_CONN` (senza, escono subito senza verificare nulla).
- T24 e T25 sono coperti **a livello di database**: backup cifrato, ripristino su database pulito dalla sola copia esterna e chiave, riapplicazione delle cancellazioni dal registro esterno. Restano da provare **su una macchina Windows pulita** e con la procedura scritta per una persona non tecnica.

Restano da scrivere con l'applicazione: interfaccia, stampa/PDF (T27 nella parte documenti), blocco dei tentativi di accesso (T19), accesso di rete a PostgreSQL (T26), cifratura applicativa dei file (T20). Nessun test dimostra la conformità giuridica: dimostra che il software si comporta come specificato.

## 1. Test di comportamento normativo (v1)

| ID | Verifica | Rif. |
|---|---|---|
| T01 | Informativa pubblicata senza segnaposto, con versione e riferimenti corretti; la pubblicazione di una versione con segnaposto o finalità non valutate è rifiutata | F02 |
| T02 | Nessuna scelta preselezionata, nessuna accettazione cumulativa nei moduli | F02 |
| T03 | Consegna senza firma registrata come «consegnata senza firma»: nessun consenso o sottoscrizione inventati | F02 |
| T04 | Rappresentante con poteri scaduti non selezionabile per una nuova consegna; consegna storica conservata nel suo contesto | §4.2 |
| T05 | Richiesta da non paziente (collaboratore, erede ex art. 2-terdecies) registrabile senza creare un paziente | F03 |
| T06 | Calcolo del termine (vedi §2 sotto) | F03 |
| T07 | Proroga registrata senza cancellare la scadenza iniziale; prova della comunicazione allegabile; chiusura impossibile senza esito e riscontro | F03 |
| T08 | Incidente di sola indisponibilità classificabile come violazione; le 72 ore sono calcolate su istanti, anche attraverso un cambio d'ora | F04 |
| T09 | Notifica al Garante non registrabile senza protocollo e ricevuta inseriti dopo l'azione effettiva | F04 |
| T10 | Rimozione di un interessato: anagrafica azzerata, `internal_code` e consegne anonimizzate conservati, file non più giustificati eliminati, audit annotato | F07 |
| T11 | Rettifica dell'anagrafica non modifica i documenti già emessi; la ricostruzione di una consegna passata dà lo stesso `rendered_hmac` | F02, F07 |
| T12 | La regola di conservazione «illimitata» richiede criterio; durata NULL senza criterio rifiutata | §4.4 |
| T13 | Sospensione della cancellazione (se già presente) impedisce l'eliminazione fino alla revisione | v1.1 |

## 2. Test di calcolo del termine (F03)

Funzione `deadline_rule_version = 1`, da confermare dal consulente sul Reg. 1182/71 prima del rilascio; ogni riga è un caso automatico. Il giorno di ricezione non conta, la scadenza è il giorno corrispondente del mese successivo.

| Ricevuta | Scadenza attesa | Motivo |
|---|---|---|
| 15 marzo | 15 aprile (se giorno lavorativo) | regola ordinaria |
| 31 gennaio | 28 febbraio (29 se bisestile) | giorno corrispondente assente → ultimo del mese |
| 30 novembre | 30 dicembre | mese con meno giorni |
| 31 dicembre | 31 gennaio | cambio d'anno |
| ricevuta il 15, scadenza di sabato/domenica | primo giorno lavorativo successivo | slittamento a giorno lavorativo |
| scadenza cade in festività nazionale (es. 25 aprile, 1° maggio, 2 giugno, 25 dicembre) | primo giorno lavorativo successivo | festività |
| ricevuta in giorno di cambio d'ora | stessa data civile, fuso Europe/Rome | nessuna deriva |
| proroga (art. 12(3)) | 3 mesi complessivi dalla ricezione, con la stessa regola (es. 31/01 → 30/04); **da confermare**: se la proroga di due mesi si somma alla scadenza già calcolata (28/02 + 2 mesi) il risultato cambia | art. 12(3) |

I casi attesi vanno **ricalcolati e approvati dal consulente**: se la regola cambia, cambia `deadline_rule_version` e restano leggibili le scadenze già calcolate.

## 3. Test di integrità e sicurezza

| ID | Verifica |
|---|---|
| T14 | Modifiche ai dati correnti non cambiano documenti emessi né il registro storico; l'esportazione del registro al tempo T non cambia |
| T15 | Due finestre (o due avvii) pubblicano la stessa revisione: una sola operazione valida, conflitto esplicito per l'altra (`row_version`) |
| T16 | Il ruolo operativo non può eseguire DDL né modificare liberamente audit e versioni pubblicate |
| T17 | La catena di hash dell'audit rileva una riga modificata o eliminata |
| T18 | Password, token e contenuti sanitari assenti da errori, log tecnici e audit |
| T19 | Il blocco dei tentativi di accesso persiste al riavvio |
| T20 | File alterato, tag errato, tipo non ammesso o dimensione eccessiva rifiutati; nessun nome di file originale conservato; HMAC dei contenuti, non hash nudo |
| T21 | Un'interruzione durante il salvataggio non lascia documento, versione, consegna ed evento incoerenti |
| T22 | Un'operazione ritentata (stesso identificatore di comando) non duplica una consegna o un documento |
| T23 | Scansione illeggibile o riferita al paziente sbagliato (`delivery_code` non corrispondente) non è classificata automaticamente come prova valida |
| T24 | Ripristino su ambiente pulito, con solo backup e materiale di recupero delle chiavi e senza accesso al PC originale |
| T25 | Dopo un ripristino non tornano visibili i dati già cancellati (`disposal_records` riapplicati) |
| T26 | PostgreSQL non è raggiungibile dall'esterno della macchina; nessuna connessione senza autenticazione |
| T27 | Nessuna chiamata di rete in uscita durante la generazione di documenti (font, immagini, script) |

## 4. Criteri di accettazione dei flussi (riepilogo)

| Flusso | Criterio |
|---|---|
| F01 Registro | Esportazione alla data T invariata dopo modifiche successive; pubblicazione rifiutata se incompleta; nessuna visita di pazienti nel registro |
| F02 Informativa | Documento ricostruito identico dopo modifica dei dati dello studio; consegna senza firma registrata come tale |
| F03 Richieste | Termine calcolato dalla funzione versionata; chiusura solo con esito e riscontro; richieste di non pazienti ammesse |
| F04 Incidenti | Conto alla rovescia di 72 ore su istanti; nessuna decisione automatica; notifica registrabile solo con ricevuta |
| F05 Conservazione | Elenco in scadenza con motivo; operazione manuale registrata senza ricreare dati; nessuna cancellazione riattivata dal ripristino |
| F06 Backup | Backup cifrato e verificato, ripristino riuscito su macchina pulita e registrato; RPO/RTO confermati dal medico |
| F07 Rimozione | Anagrafica azzerata, prova minima conservata, nessun dato eccedente residuo |

## 5. Prestazioni e usabilità

Definire un dataset sintetico realistico (almeno alcune migliaia di interessati e consegne). Misurare apertura, ricerca, stampa e backup. L'interfaccia non si blocca durante operazioni di I/O lunghe: chiamate asincrone, cancellazione controllata, messaggi comprensibili. Verificare stampa A4, margini, testi lunghi, accenti, paginazione, identificativo su ogni pagina e spazio per la firma; usabilità da tastiera e con ingrandimento; la procedura di recupero chiavi eseguita da una persona non tecnica seguendo solo il manuale.

## 6. Condizioni di rilascio con dati reali

Non si usano dati reali finché mancano:

1. configurazione effettiva del titolare (profilo, destinatari, fornitori, sistemi);
2. approvazione delle informative da parte del titolare (e, se coinvolto, del consulente);
3. durate e regole di conservazione decise e registrate (documento 1, §6);
4. regola del termine dei diritti confermata dal consulente e T06 superato;
5. decisione D-ENC (documento 2, §3) presa e rischio residuo registrato;
6. prova dei permessi (T16) e della catena di audit (T17);
7. ripristino riuscito su macchina pulita (T24, T25) e copia delle chiavi di recupero consegnata e verificata;
8. procedura incidenti scritta e provata con un caso simulato;
9. responsabilità di manutenzione e aggiornamenti assegnate, incluso il ruolo privacy dello sviluppatore se accede ai dati (documento 1, §8).

Queste sono condizioni di progetto basate sul rischio, non un certificato di conformità. Se una non è soddisfatta, si continua lo sviluppo e i test su dati sintetici.

## 7. Pacchetto di consegna

Installer o avvio documentato; versione applicativa e versione dello schema; manuale breve per il medico; esportazione dei dati; guida al backup e procedura di recupero; note di rilascio e limiti noti; rapporto dei test T01–T27 con evidenze; elenco delle decisioni aperte con responsabile e scadenza.

## 8. Test per le fasi successive

Da attivare quando si sviluppa la v1.1: ordine di precedenza degli eventi di consenso e revoca (retrodatati, in concorrenza, sovrapposti); revoca limitata alla finalità corretta; rifiuto distinto da assenza; sospensioni e lotti di cancellazione riprendibili; più utenti e ruoli, incluso un operatore disabilitato che non può più accedere. Per pgvector/NL: permessi rispettati prima di restituire contenuti, invalidazione dei chunk di versioni ritirate, nessuna risposta senza fonte, resistenza a prompt injection nei documenti.
