# Privacy Studio — Specifica funzionale e dati (primo rilascio)

Data: 4 novembre 2026. Versione 2.0. Documento 2 di 3. Presuppone *01 Analisi normativa*; i test sono in *03 Piano di test e rilascio*.

Applicazione desktop Windows, C#/.NET, PostgreSQL locale, per un singolo medico in Italia. **PostgreSQL è scelta confermata**: serve a poter interrogare in futuro, con linguaggio naturale e pgvector, i dati strutturati e documentali dello studio (§9). Questo non cambia il primo rilascio, che non installa né usa alcuna funzione di IA.

Il documento non attesta conformità: l'app aiuta a eseguire e dimostrare adempimenti, ma non conosce ciò che avviene fuori dal programma.

## 1. Perimetro del primo rilascio

**Incluso (v1):**
1. profilo del titolare e configurazione guidata;
2. registro dei trattamenti versionato ed esportabile (con destinatari, fornitori, trasferimenti, misure, conservazione);
3. informative versionate e registro delle consegne, con eventuale ricevuta firmata;
4. richieste di esercizio dei diritti con scadenze;
5. incidenti e violazioni;
6. audit minimo, backup e ripristino, esportazione documentale.

**Rinviato (v1.1 o successive, con analisi propria):** più utenti, ruoli e sessioni; registrazione di consensi specifici e revoche (§8); sospensioni e lotti di cancellazione automatizzati; sub-responsabili modellati; processo con servizio Windows e IPC; interrogazione in linguaggio naturale (§9).

**Escluso:** cartella clinica, prescrizioni, fatturazione, FSE/dossier, telemedicina, Sistema Tessera Sanitaria, firma qualificata, conservazione digitale a norma, ricerca clinica, invio automatico di notifiche al Garante, qualunque IA.

Una configurazione incompleta resta «bozza»: l'app mostra le decisioni mancanti e non esprime mai un giudizio «conforme». Non si pubblicano informative con segnaposto o finalità non valutate.

## 2. Architettura

**P**
- Windows Forms o WPF in C#/.NET; livelli UI, Application (casi d'uso, autorizzazione, transazioni), Domain (versioni, scadenze, invarianti), Infrastructure (database, file cifrati, PDF/stampa, backup).
- PostgreSQL locale con migrazioni numerate. Due ruoli: *migrazione* (DDL, usato solo dall'installer/aggiornatore) e *operativo* (DML limitato; nessun DDL; nessun UPDATE/DELETE su audit e versioni pubblicate, ottenuto con privilegi e trigger).
- **Nessun servizio Windows né IPC in v1**: le scadenze si calcolano all'avvio e all'apertura delle schede; il backup è un eseguibile da riga di comando lanciato da Utilità di pianificazione con la stessa identità dell'utente Windows del medico. Se in futuro serviranno più utenti, l'architettura va rivista, non estesa con un nuovo valore in un campo ruolo.
- Accesso: un solo utente titolare con credenziale locale (blocco schermo dell'applicazione), verificatore con algoritmo standard e libreria mantenuta, blocco persistente dei tentativi. Nessuna tabella di sessioni: la sessione vive in memoria.
- Nessun download di font, immagini o script remoti durante la generazione dei documenti. Nessuna telemetria con dati paziente. Nessun invio email, upload cloud o supporto remoto automatici. Le esportazioni chiedono una destinazione esplicita.
- L'installazione dichiara i propri limiti: chi controlla l'account Windows o il ruolo PostgreSQL può aggirare alcune restrizioni applicative.

## 3. Convenzioni dati

Salvo cataloghi e tabelle ponte: `id uuid PK`, `created_at timestamptz NOT NULL`, `created_by uuid FK users` (NULL solo per bootstrap). Le entità modificabili hanno `updated_at` e `row_version integer > 0` per il controllo di concorrenza; gli eventi non si aggiornano. `?` = NULL ammesso. Date civili in `date`, istanti in `timestamptz`.

I campi `*_code` referenziano cataloghi dedicati (`code text PK`, `label text`, `enabled boolean`); il contenuto iniziale di ogni catalogo è parte della migrazione 001 e va elencato nel DDL. Le durate non sono stringhe. FK `ON DELETE RESTRICT`/`ON UPDATE RESTRICT` per dominio, prove e storici; CASCADE solo per figli tecnici di bozze e tabelle ponte.

Poiché l'installazione è singola, **`practice_id` compare solo su `practices`, `users` e sulle entità radice**; si omette dai figli (che ereditano il perimetro dal genitore) e **non si pubblicizza alcun isolamento multi-studio**. Unicità sempre dichiarata nel proprio perimetro (es. `UNIQUE(practice_id, code)`).

**Dati sensibili e cifratura:** nessun nome di file originale è conservato (§4.5); il file riceve un nome casuale. Per l'hash dei contenuti si usa un **HMAC con chiave dell'applicazione**, non un hash nudo, così non si riconoscono file noti. I campi testuali liberi sono minimizzati e non contengono dati clinici.

**Decisione aperta D-ENC (prima dei dati reali):** protezione di anagrafica e testi liberi in PostgreSQL. Opzioni: (a) cifratura del volume e dei backup più ruolo operativo ristretto (rischio residuo: accesso dell'amministratore di sistema a macchina accesa); (b) cifratura applicativa dei campi identificativi (impedisce ricerche SQL e embedding su quei campi). Per v1 si parte da (a), esplicitando il rischio residuo nel registro delle misure; la scelta finale spetta al titolare con lo sviluppatore.

## 4. Modello dati v1

**DDL eseguibile:** `/home/mauri/progetti/Privacy/db/` — `000_bootstrap_roles.sql`, `001_schema_v1.sql` (46 tabelle e 48 cataloghi), `002_catalogs.sql` (contenuto dei cataloghi), `003_rules_and_grants.sql` (invarianti, funzioni, privilegi), `004_disposal_ledger.sql` (eliminazione fisica e registro esterno), `005_hardening.sql` (correzioni applicabili anche a un database già in uso), `tests/test_v1.sql` e `run_tests.sh`. Verificato su PostgreSQL 17: 137 controlli superati. Le sezioni seguenti restano la descrizione logica; dove il DDL differisce o aggiunge, vale il DDL. Differenze e aggiunte principali:

- `processed_commands` (idempotenza dei comandi), `local_holidays` (festività locali per le scadenze), catalogo `cat_template_variable` (variabili ammesse nei modelli, formato `{{codice}}`).
- `disposal_records.reason` e `policy_version_id` facoltativo (la rimozione di un interessato non discende da una politica); `incident_actions.protocol_reference` obbligatorio per le notifiche; `notice_deliveries.delivery_code` generato dal database.
- Funzioni: `compute_due_date`, `remove_subject` (F07), `dispose_file`, `verify_audit_chain`. Ruoli: `privacy_migrator` (proprietario, DDL) e `privacy_app` (operativo, nessun DDL, nessun UPDATE/DELETE su audit, eventi, consegne e file).
- Le invarianti sono nel database: versioni pubblicate immutabili, nascita sempre in bozza, validazione alla pubblicazione, consegne solo di versioni pubblicate con poteri di rappresentanza verificati e validi alla data, termini delle richieste modificabili solo con una proroga, chiusura solo con riscontro, incidenti chiudibili solo dopo la valutazione e immutabili una volta chiusi, audit append-only con catena di hash.
- Il ruolo proprietario (`privacy_migrator`) e i superuser possono aggirare le restrizioni applicative per manutenzione: è un limite dichiarato (§2).


### 4.1 Studio e accesso
- **practices**: `name`, `controller_name`, `controller_identifier?`, `address`, `privacy_contact`, `phone?`, `dpo_contact?`, `timezone` ('Europe/Rome'), `status_code`.
- **users**: `practice_id`, `username` (UNIQUE normalizzato per studio), `display_name`, `password_verifier` (algoritmo, versione, parametri, sale), `enabled`, `failed_attempts ≥0`, `locked_until?`, `credentials_changed_at`.

Nessun `roles`, `permissions`, `sessions` in v1.

### 4.2 Interessati
- **data_subjects**: `practice_id`, `internal_code` (UNIQUE per studio), `first_name`, `last_name`, `birth_date?`, `tax_code?`, `email?`, `phone?`, `status_code` (attivo, rimosso). Nessun obbligo di codice fiscale o contatti. Mai nome+cognome come chiave.
- **subject_roles**: `subject_id`, `role_code` (paziente, collaboratore, altro), PK composta.
- **representations**: `subject_id`, `representative_id` (entrambi `data_subjects`), `capacity_code`, `scope`, `valid_from`, `valid_until?`, `verified_at?`, `verified_by?`, `evidence_file_id?`. CHECK rappresentante ≠ rappresentato e fine ≥ inizio. Più rappresentanti simultanei ammessi.

### 4.3 Registro
- **processing_activities**: `practice_id`, `code`, `name`, `status_code`; UNIQUE(practice_id, code).
- **activity_versions**: `activity_id`, `version_number >0`, `description`, `status_code` (bozza, in verifica, pubblicata, superata), `effective_from?`, `review_due_on?`, `approved_by?`, `approved_at?`, `change_reason`; UNIQUE(activity_id, version_number). La pubblicazione richiede approvazione e dati completi e blocca il contenuto.
- **purpose_versions**: `activity_version_id`, `purpose_key uuid` (continuità fra versioni), `name`, `description`, `legal_assessment`, `consent_assessment_code` (necessario, non necessario, da valutare). UNIQUE(activity_version_id, purpose_key). Un cambiamento sostanziale richiede una nuova valutazione.
- **purpose_legal_grounds**: `purpose_version_id`, `article6_code`, `article9_code?`, `national_reference?`, `scope`, `assessment_note`. Solo i presupposti effettivamente applicabili; per dati ex art. 10 niente pubblicazione senza valutazione dedicata.
- **Cataloghi**: `data_categories` (con `classification_code`: ordinari, art9, art10, da valutare), `subject_categories`, `recipient_categories`, `article6_catalog`, `article9_catalog`.
- **Ponti per versione di attività**: `activity_data_categories`, `activity_subject_categories`, `activity_recipient_categories`, `activity_systems`, `activity_measures` (con `measure_description_snapshot`), `activity_engagements` (con `role_snapshot`). Tutte con PK composta.
- **systems**: `practice_id`, `name`, `type_code`, `location_description`, `owner_user_id?`, `status_code`.
- **security_measures**: `practice_id`, `name`, `description`, `status_code`, `implemented_on?`, `verified_on?`, `evidence_file_id?`.
- **external_parties**: `practice_id`, `name`, `identifier?`, `contact`, `status_code`.
- **service_engagements**: `party_id`, `service_name`, `privacy_role_code` (responsabile, titolare autonomo, contitolare, da valutare), `role_reason`, `valid_from`, `valid_until?`, `agreement_file_id?` (contratto allegato come file, **senza** passare da istanze documentali dei pazienti), `review_due_on`.
- **transfers**: `activity_version_id`, `engagement_id?`, `destination_country_code?`, `international_organization?`, `mechanism_code`, `assessment`, `safeguard_file_id?`, `review_due_on`. CHECK: almeno paese o organizzazione.
- **compliance_assessments**: `activity_version_id?`, `type_code` (screening DPIA, DPIA, valutazione DPO, altra), `outcome_code`, `reason`, `assessed_by`, `assessed_at`, `review_due_on`, `evidence_file_id?`.

### 4.4 Conservazione
- **retention_policy_versions**: `policy_key`, `version_number`, `record_class_code`, `trigger_code`, `duration_kind_code` (**fissa, illimitata motivata, a criterio**), `duration_months?`, `duration_days?`, `criterion`, `legal_reference`, `review_due_on`, `approved_by?`, `approved_at?`, `status_code`. CHECK: `fissa` richiede almeno una durata; `illimitata` e `a criterio` richiedono `criterion` non vuoto; durata NULL non vale mai conservazione illimitata.
- **purpose_retention_rules**: `purpose_version_id`, `data_category_code`, `policy_version_id`, PK composta.
- **disposal_records**: `performed_at`, `performed_by`, `record_class_code`, `generic_description`, `item_count`, `policy_version_id`. Resoconto minimo che non ricrea i dati eliminati.

In v1 la cancellazione è un'operazione manuale guidata dalla lista «in scadenza»; sospensioni (`document_holds`, `request_holds`) e lotti riprendibili sono v1.1.

### 4.5 Documenti e file
- **documents**: `practice_id`, `type_code`, `title`, `status_code`.
- **document_versions**: `document_id`, `version_number`, `language_code`, `body` (**testo definitivo, con i dati dello studio già risolti al momento della pubblicazione**), `status_code` (bozza, approvato, pubblicato, ritirato), `effective_from?`, `approved_by?`, `approved_at?`, `content_hmac`. UNIQUE(document_id, version_number, language_code). Variabili ammesse in un catalogo chiuso; la pubblicazione rifiuta segnaposto irrisolti. Cambiare l'indirizzo del medico richiede una nuova versione: così un modulo emesso ieri resta identico.
- **notice_activity_versions**: `document_version_id`, `activity_version_id`, PK composta. Lega il testo informativo al registro.
- **files**: `media_type`, `stored_name` (casuale), `size_bytes ≥0`, `ciphertext bytea`, `nonce`, `auth_tag`, `wrapped_key`, `key_version`, `plaintext_hmac`, `status_code`. **Nessun nome originale.** AES-GCM con nonce univoco per chiave e tag verificato prima dell'uso; chiave principale fuori dal database. Tipi e dimensioni ammessi definiti; per scansioni voluminose prevedere soglia e futuro archivio esterno cifrato con consistenza transazionale. Niente indici sul contenuto.
- **notice_deliveries** (sostituisce l'istanza compilata per paziente): `document_version_id`, `subject_id`, `representation_id?`, `delivery_code` (identificatore stampabile, UNIQUE), `delivered_at?`, `channel_code`, `receipt_status_code` (**consegnata senza firma, ricevuta firmata, rifiuto della sola firma, tentativo fallito**), `subject_name_snapshot`, `signatory_name_snapshot?`, `signatory_capacity_snapshot?`, `rendered_hmac?`, `rendered_file_id?` (solo se il titolare attiva la conservazione del PDF, opzione **C**, di default disattivata), `recorded_by`, `note?`. Il PDF si può **ricostruire** dalla versione + gli snapshot; `rendered_hmac` registra cosa è stato stampato. `delivered_at` NULL per un tentativo fallito.
- **signature_evidence**: `delivery_id`, `signed_file_id`, `signature_type_code`, `signed_at?` (data dichiarata), `acquired_at`, `verified_by?`, `verification_note?`. Non si impone una data certa inesistente.

### 4.6 Richieste
- **rights_requests**: `practice_id`, `subject_id?` (persona cui i dati si riferiscono; NULL se non ancora identificata), `requester_name?`, `requester_capacity_code` (interessato, rappresentante, erede/aventi interesse art. 2-terdecies, altro), `reference_code` (UNIQUE per studio), `received_at`, `channel_code`, `request_text`, `status_code`, `assignee_id`, `identity_status_code`, `identity_verified_at?`, `initial_due_on date`, `effective_due_on date`, `deadline_rule_version`, `closed_at?`. Non si crea un paziente fittizio per un interessato diverso.
- **request_rights**: `request_id`, `right_code`, PK composta.
- **request_events**: `request_id`, `event_type_code`, `occurred_at`, `actor_id`, `description`, `file_id?`.
- **request_extensions**: `request_id`, `new_due_on`, `reason`, `decided_at`, `notified_at?`, `notification_file_id?`. La registrazione non prova legittimità né tempestività della comunicazione.
- **request_decisions**: `request_id`, `right_code` (FK composta verso `request_rights`), `outcome_code`, `reason`, `decided_by`, `decided_at`, `response_file_id?`, `sent_at?`.

### 4.7 Incidenti
- **incidents**: `practice_id`, `reference_code`, `discovered_at`, `awareness_at?`, `notification_deadline_at?` (= `awareness_at` + 72 h, calcolata e salvata), `description`, `classification_code` (incidente tecnico, violazione, da valutare), `status_code`, `assignee_id`, `closed_at?`.
- **incident_assessments**: `incident_id`, `assessed_at`, `assessed_by`, `risk_level_code`, `reason`, `approx_subject_count?`, `approx_record_count?`, `consequences`, `notification_decision_code`, `subject_communication_decision_code`, `delay_reason?`. Più valutazioni nel tempo; ponti tipizzati per categorie e sistemi.
- **incident_actions**: `incident_id`, `type_code` (contenimento, notifica, integrazione, comunicazione, chiusura), `occurred_at`, `description`, `actor_id`, `evidence_file_id?`. Protocollo e ricevuta della notifica si registrano solo dopo l'azione effettiva.

### 4.8 Audit, backup, migrazioni
- **audit_events**: `id bigint identity`, `occurred_at`, `actor_id?`, `action_code`, `outcome_code`, `correlation_id`, `target_type`, `target_id?` (riferimento volutamente non vincolato per sopravvivere alle cancellazioni), `reason_code?`, **`prev_hash bytea`, `row_hash bytea`** (catena di hash: rende rilevabile la manomissione, non la impedisce). Mai password, token, documenti, query o parametri sensibili.
- **backup_runs**: `started_at`, `finished_at?`, `status_code`, `artifact_reference?`, `key_version?`, `verified_at?`, `error_code?`.
- **restore_tests**: `backup_id`, `started_at`, `finished_at?`, `outcome_code`, `report`, `performed_by`.
- **schema_migrations**: `version PK`, `checksum`, `applied_at`, `application_version`.

### 4.9 Indici e integrità
Indici parziali su richieste aperte per `effective_due_on`, attività da rivedere, consegne per soggetto e data, audit per data e attore. Gli indici di ricerca con dati personali fanno parte del trattamento e del backup. Le pubblicazioni sono transazioni con blocco della versione. Coerenza fra soggetto, rappresentante e documento garantita da FK composte o controlli transazionali documentati.

## 5. Flussi e criteri di accettazione

### F01 — Registro
Creare attività e finalità; associare categorie, destinatari, sistemi, misure, conservazione; valutare basi e trasferimenti; validare; approvare; pubblicare una revisione immutabile; per ogni modifica creare una nuova revisione che copia le associazioni; esportare il registro alla data T.

**Accettazione:** l'esportazione al tempo T non cambia se poi cambia un fornitore o una finalità. Il registro non elenca le visite dei pazienti. La pubblicazione è rifiutata se mancano campi obbligatori.

### F02 — Informativa e registro delle consegne
1. Selezionare una versione pubblicata e pertinente.
2. Selezionare l'interessato ed eventuale rappresentante con poteri verificati.
3. Generare il modulo (testo della versione + nome, data, `delivery_code`), con anteprima, stampare o salvare il PDF.
4. Registrare separatamente generazione, stampa, esito della consegna e, se esiste, la ricevuta firmata; la stampa non prova la consegna.
5. Acquisire la scansione, verificarne leggibilità e corrispondenza al `delivery_code`.

Il modulo ha intestazione, contatti privacy, testo approvato, versione e identificativo su ogni pagina, data, spazio per la ricevuta. Nessuna scelta preselezionata, nessuna accettazione cumulativa. Paziente e firmatario sono distinti.

**Accettazione:** dopo la modifica dell'indirizzo del medico, il documento ricostruito da una consegna passata è identico a quello emesso (stesso `rendered_hmac`). Se il paziente riceve ma non firma, si registra «consegnata senza firma» senza inventare sottoscrizioni né consensi.

### F03 — Richieste dei diritti
Accettare da qualunque canale; identificare con misure proporzionate; classificare i diritti; assegnare responsabile e scadenza; decidere e motivare; predisporre il riscontro; registrare invio e prove.

**Calcolo del termine** (funzione versionata `deadline_rule_version`, fuso Europe/Rome): *regola proposta, da confermare dal consulente sul Reg. 1182/71*: il termine inizia il giorno successivo alla ricezione e scade nel giorno corrispondente del mese successivo; se manca (es. 31 → febbraio) scade l'ultimo giorno del mese; se scade di sabato, domenica o festività nazionale, slitta al primo giorno lavorativo. Ogni modifica manuale richiede motivazione. Proroga, chiarimento e dubbio sull'identità sono eventi distinti: nessuna sospensione implicita.

**Accettazione:** non si chiude senza esito, motivazione e riscontro documentato (salvo annullamento motivato). Le richieste di collaboratori, eredi o altri non richiedono un paziente. I casi di test del termine sono nel documento 3.

### F04 — Incidenti
Registrare; contenere; valutare se riguarda dati personali; distinguere scoperta e conoscenza; valutare il rischio; decidere su notifica e comunicazioni; registrare interventi. Il sistema avvisa con il conto alla rovescia di 72 ore ma non decide.

**Accettazione:** un incidente di sola indisponibilità può essere classificato come violazione; la notifica è registrata solo con protocollo e ricevuta inseriti dopo l'invio effettivo; il conto delle 72 ore usa istanti (corretto anche con cambio d'ora).

### F05 — Conservazione e cancellazione (v1 manuale)
Implementazione: migrazione `db/004_disposal_ledger.sql`. Ogni eliminazione scrive in `disposal_items` solo tipo e identificativo dell'elemento; l'applicazione ne tiene una **copia fuori dal database** (file accodato, da conservare con i backup, in luogo separato) perché un ripristino da backup più vecchio riporterebbe anche i `disposal_records` più recenti. La procedura `dispose_item` elimina fisicamente (richieste e incidenti solo se chiusi) ed è l'unico modo: il ruolo operativo non cancella direttamente dalle tabelle di eventi.
L'app elenca elementi in scadenza con motivo e dipendenze; il titolare autorizza; l'operazione elimina record, allegati e copie gestite, e registra un `disposal_record` che non ricrea i dati. I backup hanno rotazione propria; dopo un ripristino un'apposita procedura riapplica le cancellazioni già registrate.

**Accettazione:** dopo un ripristino da backup precedente a una cancellazione, i dati cancellati non tornano visibili (la procedura riapplica i `disposal_records`).

### F06 — Backup e ripristino
Backup pianificato, cifrato, con esito verificabile; una copia fuori dal dispositivo primario; copre database, allegati, configurazione, riferimenti alle chiavi. Ripristino periodico in ambiente isolato. Proposta iniziale **RPO 24 h, RTO 1 giorno lavorativo**, da confermare con il medico (non sono termini del GDPR).

**Accettazione:** il ripristino su una macchina pulita, usando solo backup e materiale di recupero delle chiavi, riporta il sistema operativo; l'esito è registrato in `restore_tests`.

### F07 — Cancellazione e rettifica di un interessato
Procedura di «rimozione interessato» (previa valutazione del titolare sull'applicabilità dell'art. 17 e sui vincoli di conservazione): sostituisce nome e cognome con «Interessato rimosso», azzera data di nascita, codice fiscale, contatti; mantiene `id`, `internal_code`, `status_code='rimosso'` e le consegne come eventi anonimizzati (snapshot dei nomi azzerati); elimina eventuali PDF e scansioni non più giustificati; registra l'operazione in audit e `disposal_records`. Rettifica: aggiorna l'anagrafica corrente; i documenti passati restano ricostruibili con il testo emesso, annotando la rettifica, e vengono rigenerati solo su decisione motivata del titolare.

## 6. Stati
- Attività: bozza → in verifica → pubblicata → superata (la storia non si cancella).
- Documento: bozza → approvato → pubblicato → ritirato (non genera nuovi moduli salvo riproduzione motivata).
- Consegna: derivata da `receipt_status_code`; firma e acquisizione sono eventi distinti.
- Richiesta: ricevuta → verifica → istruttoria → riscontro → chiusa; proroghe e attese sono eventi.
- Incidente: segnalato → valutazione → contenimento/azioni → chiuso, con nuova valutazione possibile.

Ogni comando verifica identità, permesso, stato ammesso, versione attesa (`row_version`) e dati necessari. Le operazioni ripetute da retry sono idempotenti tramite identificatore di comando, soprattutto per generazione documenti, acquisizione di prove e registrazione di consegne.

## 7. Crittografia, chiavi, stampa
Modello di minaccia da definire prima della scelta definitiva: PC spento rubato, dump sottratto, backup perso, operatore non autorizzato, malware nell'account attivo, amministratore compromesso. Nessuna misura li copre tutti.

Proposta: cifratura del volume, backup cifrati, cifratura autenticata degli allegati con chiave per file avvolta da chiave principale. La chiave principale è protetta da Windows (DPAPI o dispositivo appropriato) con **procedura separata di recupero** consegnata al medico (copia fuori dal PC). Si dichiara che senza il materiale di recupero non è possibile ripristinare file né password: la perdita delle chiavi è un rischio di disponibilità da trattare come prioritario (formazione, copia verificata, test T20).

Connessioni a PostgreSQL solo locali o con TLS e verifica del certificato; nessuna esposizione pubblica; autenticazione SCRAM. Spooler, PDF esportati e fogli stampati sono copie fuori dal database: file temporanei con ACL restrittive, pulizia controllata, nessuna persistenza inutile. Non dichiarare cancellazione sicura da SSD per la sola rimozione del file.

## 8. Rinviato: consensi specifici (v1.1)
Se una finalità richiede un consenso specifico (es. comunicazioni promozionali, ricerca), **non usare v1 per registrarlo**. Per la v1.1 si prevede: `consent_clauses` (clausola per versione di documento e finalità), `consent_events` (scelta/revoca/rifiuto/rettifica, `occurred_at`, `recorded_at`, `sequence_number`, firmatario, rappresentanza, prova, `supersedes_event_id`). **Regola d'ordine da fissare prima di implementare**: lo stato corrente segue l'ordine di registrazione (`sequence_number`) oppure l'istante dichiarato (`occurred_at`)? Il caso critico è una revoca registrata in ritardo ma avvenuta prima di un nuovo consenso: la scelta va decisa con il consulente e coperta da test. Una revoca non interrompe sistemi esterni: genera attività assegnate e verificabili.

## 9. Predisposizione a pgvector e linguaggio naturale
Il primo rilascio non installa pgvector, non crea embedding e non espone interrogazioni in linguaggio naturale. Predispone solo: identificatori stabili, documenti versionati, viste di sola lettura documentate, assenza di testi clinici liberi.

Prima di attivarla serve un'**analisi del trattamento** e un aggiornamento del registro (nuova attività «interrogazione assistita»), con valutazione DPIA e dei trasferimenti se si usa un servizio esterno (preferire modello locale). Indicazioni tecniche:

- primo ambito: registro, informative, procedure e documentazione dello studio, **non** anagrafica o dati dei pazienti; gli embedding sono dati personali derivati se generati da testi con dati personali, e vanno cancellati con i sorgenti;
- schema separato (`nl`) con ruolo di sola lettura; `document_chunks(id, document_version_id FK, ordinal, text_hash, extraction_version)` e tabella degli embedding con FK al chunk, modello, versione, dimensione, data, vettore; dimensione e indici scelti dopo la verifica della versione pgvector;
- le domande strutturate passano da viste e operazioni parametrizzate: niente SQL arbitrario né credenziali di scrittura al modello;
- le risposte mostrano fonti e data dei dati, limiti su righe e tempi, nessuna risposta inventata se non ci sono risultati; valutare prompt injection nei documenti;
- invalidare i chunk delle versioni ritirate e cancellare i derivati secondo la conservazione.

## 10. Migrazione da un eventuale prototipo
Accertare prima se il prototipo sia stato usato con dati propri; non assumerne l'assenza. Poi: inventario e backup verificato; migrazione numerata con checksum; conversione in area di staging senza toccare le sorgenti; mappatura di campi e stati, con date esatte e sconosciuti in coda di revisione; riconciliazione manuale degli interessati (mai per somiglianza di nome); conservazione della provenienza e delle incertezze; confronto di conteggi e campioni, validazione di vincoli, rapporto degli scarti; passaggio controllato con rollback; eliminazione dello staging dopo verifica. Un'annotazione importata non diventa prova firmata, né una data incerta un evento certificato. La tabella `migration_issues` (`source_entry_id`, `source_revision`, `issue_code`, `description`, `status_code`, `resolved_by?`, `resolved_at?`) esiste **solo se** la migrazione è necessaria.

## 11. Fasi di sviluppo
- **A — Fondazioni:** progetto modulare, migrazioni, PostgreSQL di test, accesso del titolare, transazioni, dati fittizi.
- **B — Registro e informative:** attività versionate, informative approvate, consegne, PDF/stampa, prove; cifratura e recupero chiavi prima di allegati reali.
- **C — Processi:** richieste e scadenze, incidenti, conservazione manuale, fornitori/destinatari.
- **D — Esercizio:** audit con catena di hash, backup/ripristino, migrazione se necessaria, test di sicurezza, manuale breve.

## 12. Istruzioni per chi sviluppa
Usare questo documento come base da validare, non come autorizzazione a inventare risposte mancanti. Le decisioni di legge restano configurabili e motivate; i punti rimasti aperti sono quelli indicati come «decisione aperta», «da confermare» o «rinviato», e vanno chiusi con il medico e il consulente, non nel codice. Prima di modificare dati esistenti, leggere il progetto e accertare lo stato del database.

Produrre il DDL di §4 (con i contenuti dei cataloghi), le migrazioni provate su PostgreSQL di test, i test delle invarianti e dei casi del documento 3. Non usare JSON al posto di pazienti, finalità, date, stati o FK: JSON solo per metadati tecnici non essenziali, con schema e limiti. Generare in ordine servizi applicativi, accesso ai dati, motore documentale, interfaccia, verificando l'autorizzazione in ogni comando. Non dichiarare completate stampa, cifratura, scadenze, backup o conformità sulla sola compilazione: consegnare evidenze di funzionamento e limiti residui.

Responsabilità: il **medico** decide specializzazione, personale, minori, destinatari, canali, durate, uso del PDF per paziente, e approva le informative; il **consulente** verifica basi, contenuti, ruoli esterni, DPIA/DPO, regola del termine; lo **sviluppatore** documenta framework, cifratura, recupero chiavi, libreria PDF e licenza, versione PostgreSQL supportata, logging, aggiornamenti, packaging e, se accede ai dati, il proprio ruolo privacy (vedi documento 1, §8).
