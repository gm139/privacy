-- 002_catalogs.sql — contenuto iniziale dei cataloghi (codici stabili, etichette traducibili).
-- I codici non cambiano mai; nuove voci con una nuova migrazione.
-- I contenuti giuridici (basi, condizioni, categorie) sono un ELENCO DI SCELTA, non una valutazione:
-- l'applicabilità al caso concreto la decide il titolare.
BEGIN;
SET LOCAL search_path = ps, pg_catalog;

INSERT INTO cat_practice_status VALUES ('draft','Configurazione in bozza',true),('active','Attiva',true);
INSERT INTO cat_record_status   VALUES ('active','Attivo',true),('archived','Archiviato',true);
INSERT INTO cat_subject_status  VALUES ('active','Attivo',true),('removed','Rimosso (dati azzerati, prova minima conservata)',true);
INSERT INTO cat_subject_role    VALUES ('patient','Paziente',true),('staff','Collaboratore o dipendente',true),('other','Altro interessato',true);
INSERT INTO cat_representation_capacity VALUES
  ('parental_responsibility','Responsabilità genitoriale',true),
  ('guardian','Tutore',true),
  ('support_administrator','Amministratore di sostegno',true),
  ('delegate','Delegato',true),
  ('other','Altro',true);

INSERT INTO cat_activity_status VALUES
  ('draft','Bozza',true),('in_review','In verifica',true),('published','Pubblicata',true),('superseded','Superata',true);
INSERT INTO cat_consent_assessment VALUES
  ('to_assess','Da valutare',true),('required','Consenso necessario',true),('not_required','Consenso non necessario',true);

INSERT INTO cat_article6 VALUES
  ('consent','Art. 6(1)(a) — consenso',true),
  ('contract','Art. 6(1)(b) — esecuzione di un contratto o misure precontrattuali',true),
  ('legal_obligation','Art. 6(1)(c) — obbligo legale',true),
  ('vital_interests','Art. 6(1)(d) — interessi vitali',true),
  ('public_task','Art. 6(1)(e) — compito di interesse pubblico o pubblici poteri',true),
  ('legitimate_interests','Art. 6(1)(f) — legittimo interesse',true);
INSERT INTO cat_article9 VALUES
  ('explicit_consent','Art. 9(2)(a) — consenso esplicito',true),
  ('employment_social_security','Art. 9(2)(b) — diritto del lavoro e sicurezza sociale',true),
  ('vital_interests','Art. 9(2)(c) — interessi vitali',true),
  ('nonprofit_body','Art. 9(2)(d) — organismi senza scopo di lucro',true),
  ('made_public','Art. 9(2)(e) — dati resi manifestamente pubblici',true),
  ('legal_claims','Art. 9(2)(f) — accertare, esercitare o difendere un diritto',true),
  ('substantial_public_interest','Art. 9(2)(g) — interesse pubblico rilevante',true),
  ('health_care','Art. 9(2)(h) con art. 9(3) — cura, diagnosi, assistenza, gestione servizi sanitari',true),
  ('public_health','Art. 9(2)(i) — sanità pubblica',true),
  ('research_archiving','Art. 9(2)(j) — archiviazione, ricerca, statistica',true);

INSERT INTO cat_data_classification VALUES
  ('ordinary','Dati ordinari',true),('art9','Categorie particolari (art. 9)',true),
  ('art10','Condanne penali e reati (art. 10)',true),('to_assess','Da valutare',true);

INSERT INTO cat_data_category (code, label, enabled, classification_code) VALUES
  ('identification','Dati anagrafici e identificativi',true,'ordinary'),
  ('contact','Dati di contatto',true,'ordinary'),
  ('health_relation','Dati sulla salute o desumibili dal rapporto con il medico (appuntamenti, prestazioni, moduli)',true,'art9'),
  ('special_other','Altre categorie particolari',true,'art9'),
  ('criminal','Condanne penali e reati',true,'art10'),
  ('administrative','Dati amministrativi e organizzativi dello studio',true,'ordinary'),
  ('staff_data','Dati di collaboratori e dipendenti',true,'ordinary'),
  ('technical_logs','Dati tecnici e registri di accesso',true,'ordinary');

INSERT INTO cat_subject_category VALUES
  ('patients','Pazienti',true),('minors','Minori',true),('representatives','Rappresentanti e delegati',true),
  ('staff','Collaboratori e dipendenti',true),('suppliers','Fornitori e loro referenti',true),('requesters','Richiedenti diritti',true);
INSERT INTO cat_recipient_category VALUES
  ('none_internal','Nessuno oltre al titolare e alle persone autorizzate',true),
  ('it_provider','Fornitore di assistenza informatica o hosting',true),
  ('professionals','Altri professionisti sanitari',true),
  ('accountant','Commercialista o consulente fiscale',true),
  ('public_bodies','Enti pubblici e autorità (ASL, Garante, altri)',true),
  ('patients_reps','Pazienti e loro rappresentanti',true),
  ('other','Altri destinatari',true);

INSERT INTO cat_system_type VALUES
  ('paper','Archivio cartaceo',true),('pc','Computer dello studio',true),('server','Server locale',true),
  ('cloud','Servizio cloud',true),('mobile','Dispositivo mobile',true),('other','Altro',true);
INSERT INTO cat_system_status VALUES ('active','In uso',true),('retired','Dismesso',true);
INSERT INTO cat_measure_status VALUES
  ('planned','Pianificata',true),('implemented','Attuata',true),('verified','Attuata e verificata',true),('not_applicable','Non applicabile',true);
INSERT INTO cat_party_status VALUES ('active','Attivo',true),('ended','Cessato',true);
INSERT INTO cat_privacy_role VALUES
  ('to_assess','Da valutare',true),('processor','Responsabile del trattamento',true),
  ('independent_controller','Titolare autonomo',true),('joint_controller','Contitolare',true);
INSERT INTO cat_transfer_mechanism VALUES
  ('to_assess','Da valutare',true),('adequacy','Decisione di adeguatezza',true),
  ('scc','Clausole contrattuali standard',true),('bcr','Norme vincolanti d''impresa',true),
  ('derogation','Deroga (art. 49)',true),('other','Altro strumento',true);
INSERT INTO cat_assessment_type VALUES
  ('dpia_screening','Screening DPIA',true),('dpia','Valutazione d''impatto (DPIA)',true),
  ('dpo_assessment','Valutazione sulla nomina del DPO',true),('other','Altra valutazione',true);
INSERT INTO cat_assessment_outcome VALUES
  ('required','Necessaria',true),('not_required','Non necessaria, motivata',true),('to_review','Da riesaminare',true);

INSERT INTO cat_record_class VALUES
  ('clinical','Documentazione clinica',true),('fiscal','Documentazione fiscale',true),
  ('notice_proof','Informative e prove di consegna',true),('requests','Richieste degli interessati',true),
  ('incidents','Incidenti e violazioni',true),('logs','Registri di audit e tecnici',true),
  ('backups','Copie di backup',true),('other','Altro',true);
INSERT INTO cat_retention_trigger VALUES
  ('last_visit','Ultima prestazione',true),('document_date','Data del documento',true),
  ('request_closed','Chiusura della richiesta',true),('incident_closed','Chiusura dell''incidente',true),
  ('contract_end','Fine del rapporto o contratto',true),('creation','Data di creazione',true),('other','Altro evento',true);
INSERT INTO cat_duration_kind VALUES
  ('fixed','Durata determinata',true),('indefinite','Illimitata, motivata',true),('criterion','Determinata da un criterio',true);
INSERT INTO cat_retention_status VALUES ('draft','Bozza',true),('approved','Approvata',true),('superseded','Superata',true);

INSERT INTO cat_document_type VALUES
  ('notice','Informativa ai pazienti',true),('authorization_instructions','Istruzioni alle persone autorizzate',true),('other','Altro documento',true);
INSERT INTO cat_document_status VALUES
  ('draft','Bozza',true),('approved','Approvato',true),('published','Pubblicato',true),('withdrawn','Ritirato',true);
INSERT INTO cat_language VALUES ('it','Italiano',true),('en','English',true);
INSERT INTO cat_file_status VALUES ('active','Presente',true),('disposed','Eliminato (contenuto rimosso)',true);
-- Variabili ammesse nei modelli: catalogo chiuso, risolte alla PUBBLICAZIONE (testo {{codice}}).
INSERT INTO cat_template_variable VALUES
  ('practice_name','Denominazione dello studio',true),('controller_name','Titolare del trattamento',true),
  ('controller_identifier','Identificativo del titolare',true),('address','Indirizzo',true),
  ('privacy_contact','Contatto privacy',true),('phone','Telefono',true),('dpo_contact','Contatto DPO',true),
  ('document_version','Versione del documento',true),('effective_date','Data di efficacia',true);

INSERT INTO cat_delivery_channel VALUES
  ('in_person','Di persona',true),('email','Email',true),('post','Posta',true),('other','Altro',true);
INSERT INTO cat_receipt_status VALUES
  ('delivered_unsigned','Consegnata senza firma',true),('signed_receipt','Ricevuta firmata',true),
  ('signature_refused','Firma rifiutata (consegna avvenuta)',true),('failed_attempt','Tentativo fallito',true);
INSERT INTO cat_signature_type VALUES ('wet_scan','Firma autografa, scansione',true),('other','Altra modalità non qualificata',true);

INSERT INTO cat_request_channel VALUES
  ('in_person','Di persona',true),('email','Email',true),('pec','PEC',true),('post','Posta',true),('phone','Telefono',true),('other','Altro',true);
INSERT INTO cat_request_status VALUES
  ('received','Ricevuta',true),('verification','In verifica',true),('inquiry','In istruttoria',true),
  ('response','Riscontro in preparazione',true),('closed','Chiusa',true),('cancelled','Annullata',true);
INSERT INTO cat_identity_status VALUES
  ('not_verified','Non verificata',true),('doubt','Dubbio concreto',true),('verified','Verificata',true);
INSERT INTO cat_requester_capacity VALUES
  ('data_subject','Interessato',true),('representative','Rappresentante o delegato',true),
  ('heir_or_interested','Erede o avente interesse (art. 2-terdecies Codice)',true),('other','Altro',true);
INSERT INTO cat_right VALUES
  ('access','Accesso (art. 15)',true),('rectification','Rettifica (art. 16)',true),('erasure','Cancellazione (art. 17)',true),
  ('restriction','Limitazione (art. 18)',true),('portability','Portabilità (art. 20)',true),('objection','Opposizione (art. 21)',true);
INSERT INTO cat_request_event_type VALUES
  ('received','Ricezione',true),('identity_check','Verifica identità',true),('clarification_requested','Chiarimenti richiesti',true),
  ('clarification_received','Chiarimenti ricevuti',true),('extension','Proroga',true),('decision','Decisione',true),
  ('response_sent','Riscontro inviato',true),('note','Nota',true),('cancelled','Annullamento',true);
INSERT INTO cat_request_outcome VALUES
  ('granted','Accolta',true),('partially_granted','Accolta in parte',true),('denied','Respinta',true),('not_applicable','Non applicabile',true);

INSERT INTO cat_incident_classification VALUES
  ('to_assess','Da valutare',true),('technical','Incidente tecnico senza dati personali',true),('breach','Violazione di dati personali',true);
INSERT INTO cat_incident_status VALUES
  ('reported','Segnalato',true),('assessment','In valutazione',true),('containment','Contenimento e azioni',true),('closed','Chiuso',true);
INSERT INTO cat_risk_level VALUES
  ('unlikely','Rischio improbabile',true),('risk','Rischio per i diritti e le libertà',true),('high','Rischio elevato',true);
INSERT INTO cat_notification_decision VALUES
  ('pending','Da decidere',true),('notify','Notificare al Garante',true),('not_notify','Non notificare, motivato',true);
INSERT INTO cat_subject_comm_decision VALUES
  ('pending','Da decidere',true),('communicate','Comunicare agli interessati',true),('not_communicate','Non comunicare, motivato',true);
INSERT INTO cat_incident_action_type VALUES
  ('containment','Contenimento',true),('notification','Notifica al Garante',true),('integration','Integrazione della notifica',true),
  ('communication','Comunicazione agli interessati',true),('closure','Chiusura',true),('note','Nota',true);

INSERT INTO cat_backup_status VALUES ('running','In corso',true),('ok','Riuscito',true),('failed','Fallito',true);
INSERT INTO cat_restore_outcome VALUES ('ok','Riuscito',true),('partial','Parziale',true),('failed','Fallito',true);

COMMIT;
