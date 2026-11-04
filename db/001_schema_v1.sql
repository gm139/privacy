-- 001_schema_v1.sql — Privacy Studio, schema primo rilascio (v1)
-- Riferimento: documento 02 «Specifica funzionale e dati», §4.
-- Eseguire come ruolo privacy_migrator.
-- Convenzioni: uuid per identità, date per date civili, timestamptz per istanti,
-- cataloghi cat_* (code text PK), FK ON DELETE RESTRICT salvo figli tecnici di bozze.

BEGIN;

CREATE SCHEMA IF NOT EXISTS ps;
SET LOCAL search_path = ps, pg_catalog;

-- ---------------------------------------------------------------- tipi base
CREATE DOMAIN nonblank AS text CHECK (btrim(VALUE) <> '');

-- ---------------------------------------------------------------- cataloghi
DO $$
DECLARE c text;
BEGIN
  FOREACH c IN ARRAY ARRAY[
    'practice_status','record_status','subject_status','subject_role','representation_capacity',
    'activity_status','consent_assessment','article6','article9','data_classification',
    'subject_category','recipient_category',
    'system_type','system_status','measure_status','party_status','privacy_role','transfer_mechanism',
    'assessment_type','assessment_outcome',
    'record_class','retention_trigger','duration_kind','retention_status',
    'document_type','document_status','language','file_status','template_variable',
    'delivery_channel','receipt_status','signature_type',
    'request_channel','request_status','identity_status','requester_capacity','right','request_event_type','request_outcome',
    'incident_classification','incident_status','risk_level','notification_decision','subject_comm_decision','incident_action_type',
    'backup_status','restore_outcome'
  ] LOOP
    EXECUTE format(
      'CREATE TABLE %I (code text PRIMARY KEY CHECK (code ~ ''^[a-z0-9_]+$''), label nonblank NOT NULL, enabled boolean NOT NULL DEFAULT true)',
      'cat_' || c);
  END LOOP;
END $$;

-- categorie di dati: catalogo con classificazione (ordinari, art. 9, art. 10, da valutare)
CREATE TABLE cat_data_category (
  code text PRIMARY KEY CHECK (code ~ '^[a-z0-9_]+$'),
  label nonblank NOT NULL,
  enabled boolean NOT NULL DEFAULT true,
  classification_code text NOT NULL REFERENCES cat_data_classification(code)
);

-- ---------------------------------------------------------------- basi per LIKE
CREATE TABLE _base (
  id         uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  created_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid
);
CREATE TABLE _mut (
  updated_at timestamptz NOT NULL DEFAULT now(),
  row_version integer    NOT NULL DEFAULT 1 CHECK (row_version > 0)
);

-- ---------------------------------------------------------------- studio e accesso
CREATE TABLE practices (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  name                  nonblank NOT NULL,
  controller_name       nonblank NOT NULL,
  controller_identifier text,
  address               nonblank NOT NULL,
  privacy_contact       nonblank NOT NULL,
  phone                 text,
  dpo_contact           text,
  timezone              text NOT NULL DEFAULT 'Europe/Rome',
  status_code           text NOT NULL DEFAULT 'draft' REFERENCES cat_practice_status(code)
);

CREATE TABLE users (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  practice_id            uuid NOT NULL REFERENCES practices(id) ON DELETE RESTRICT,
  username               nonblank NOT NULL,
  username_norm          text GENERATED ALWAYS AS (lower(btrim(username))) STORED,
  display_name           nonblank NOT NULL,
  password_verifier      nonblank NOT NULL,   -- formato standard: algoritmo$versione$parametri$sale$hash
  enabled                boolean NOT NULL DEFAULT true,
  failed_attempts        integer NOT NULL DEFAULT 0 CHECK (failed_attempts >= 0),
  locked_until           timestamptz,
  credentials_changed_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (practice_id, username_norm)
);

-- ---------------------------------------------------------------- file cifrati
CREATE TABLE files (
  LIKE _base INCLUDING ALL,
  media_type     text NOT NULL CHECK (media_type IN ('application/pdf','image/png','image/jpeg','image/tiff')),
  stored_name    text NOT NULL UNIQUE DEFAULT replace(gen_random_uuid()::text, '-', ''),  -- mai il nome originale
  size_bytes     bigint NOT NULL CHECK (size_bytes >= 0 AND size_bytes <= 26214400),      -- 25 MiB, parametro di progetto
  ciphertext     bytea,
  nonce          bytea CHECK (nonce IS NULL OR length(nonce) = 12),
  auth_tag       bytea CHECK (auth_tag IS NULL OR length(auth_tag) = 16),
  wrapped_key    bytea,
  key_version    text,
  plaintext_hmac bytea CHECK (plaintext_hmac IS NULL OR length(plaintext_hmac) = 32),     -- HMAC con chiave dell'app, non hash nudo
  status_code    text NOT NULL DEFAULT 'active' REFERENCES cat_file_status(code),
  CHECK (status_code = 'disposed' OR (ciphertext IS NOT NULL AND nonce IS NOT NULL AND auth_tag IS NOT NULL
         AND wrapped_key IS NOT NULL AND key_version IS NOT NULL AND plaintext_hmac IS NOT NULL))
);

-- ---------------------------------------------------------------- interessati e rappresentanza
CREATE TABLE data_subjects (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  practice_id   uuid NOT NULL REFERENCES practices(id) ON DELETE RESTRICT,
  internal_code nonblank NOT NULL,
  first_name    nonblank NOT NULL,
  last_name     nonblank NOT NULL,
  birth_date    date,
  tax_code      text,
  email         text,
  phone         text,
  status_code   text NOT NULL DEFAULT 'active' REFERENCES cat_subject_status(code),
  UNIQUE (practice_id, internal_code)
);

CREATE TABLE subject_roles (
  subject_id uuid NOT NULL REFERENCES data_subjects(id) ON DELETE RESTRICT,
  role_code  text NOT NULL REFERENCES cat_subject_role(code),
  PRIMARY KEY (subject_id, role_code)
);

CREATE TABLE representations (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  subject_id        uuid NOT NULL REFERENCES data_subjects(id) ON DELETE RESTRICT,
  representative_id uuid NOT NULL REFERENCES data_subjects(id) ON DELETE RESTRICT,
  capacity_code     text NOT NULL REFERENCES cat_representation_capacity(code),
  scope             nonblank NOT NULL,
  valid_from        date NOT NULL,
  valid_until       date,
  verified_at       timestamptz,
  verified_by       uuid REFERENCES users(id),
  evidence_file_id  uuid REFERENCES files(id),
  CHECK (representative_id <> subject_id),
  CHECK (valid_until IS NULL OR valid_until >= valid_from),
  CHECK ((verified_at IS NULL) = (verified_by IS NULL)),
  UNIQUE (id, subject_id)
);

-- ---------------------------------------------------------------- registro dei trattamenti
CREATE TABLE processing_activities (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  practice_id uuid NOT NULL REFERENCES practices(id) ON DELETE RESTRICT,
  code        nonblank NOT NULL,
  name        nonblank NOT NULL,
  status_code text NOT NULL DEFAULT 'active' REFERENCES cat_record_status(code),
  UNIQUE (practice_id, code)
);

CREATE TABLE activity_versions (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  activity_id    uuid NOT NULL REFERENCES processing_activities(id) ON DELETE RESTRICT,
  version_number integer NOT NULL CHECK (version_number > 0),
  description    text,                      -- obbligatoria alla pubblicazione, non in bozza
  status_code    text NOT NULL DEFAULT 'draft' REFERENCES cat_activity_status(code),
  effective_from date,
  review_due_on  date,
  approved_by    uuid REFERENCES users(id),
  approved_at    timestamptz,
  change_reason  text,
  CHECK ((approved_by IS NULL) = (approved_at IS NULL)),
  UNIQUE (activity_id, version_number)
);
CREATE UNIQUE INDEX uq_activity_one_published ON activity_versions (activity_id) WHERE status_code = 'published';

CREATE TABLE purpose_versions (
  LIKE _base INCLUDING ALL,
  activity_version_id     uuid NOT NULL REFERENCES activity_versions(id) ON DELETE CASCADE,
  purpose_key             uuid NOT NULL DEFAULT gen_random_uuid(),  -- continuità della finalità fra versioni
  name                    nonblank NOT NULL,
  description             text,
  legal_assessment        text,
  consent_assessment_code text NOT NULL DEFAULT 'to_assess' REFERENCES cat_consent_assessment(code),
  UNIQUE (activity_version_id, purpose_key)
);

CREATE TABLE purpose_legal_grounds (
  LIKE _base INCLUDING ALL,
  purpose_version_id uuid NOT NULL REFERENCES purpose_versions(id) ON DELETE CASCADE,
  article6_code      text NOT NULL REFERENCES cat_article6(code),
  article9_code      text REFERENCES cat_article9(code),
  national_reference text,
  scope              nonblank NOT NULL,
  assessment_note    nonblank NOT NULL
);
CREATE UNIQUE INDEX uq_purpose_ground ON purpose_legal_grounds
  (purpose_version_id, article6_code, coalesce(article9_code, ''), scope);

CREATE TABLE systems (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  practice_id          uuid NOT NULL REFERENCES practices(id) ON DELETE RESTRICT,
  name                 nonblank NOT NULL,
  type_code            text NOT NULL REFERENCES cat_system_type(code),
  location_description nonblank NOT NULL,
  owner_user_id        uuid REFERENCES users(id),
  status_code          text NOT NULL DEFAULT 'active' REFERENCES cat_system_status(code)
);

CREATE TABLE security_measures (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  practice_id      uuid NOT NULL REFERENCES practices(id) ON DELETE RESTRICT,
  name             nonblank NOT NULL,
  description      nonblank NOT NULL,
  status_code      text NOT NULL DEFAULT 'planned' REFERENCES cat_measure_status(code),
  implemented_on   date,
  verified_on      date,
  evidence_file_id uuid REFERENCES files(id)
);

CREATE TABLE external_parties (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  practice_id uuid NOT NULL REFERENCES practices(id) ON DELETE RESTRICT,
  name        nonblank NOT NULL,
  identifier  text,
  contact     nonblank NOT NULL,
  status_code text NOT NULL DEFAULT 'active' REFERENCES cat_party_status(code)
);

CREATE TABLE service_engagements (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  party_id          uuid NOT NULL REFERENCES external_parties(id) ON DELETE RESTRICT,
  service_name      nonblank NOT NULL,
  privacy_role_code text NOT NULL DEFAULT 'to_assess' REFERENCES cat_privacy_role(code),
  role_reason       nonblank NOT NULL,
  valid_from        date NOT NULL,
  valid_until       date,
  agreement_file_id uuid REFERENCES files(id),   -- il contratto è un file, non un'istanza di documento paziente
  review_due_on     date NOT NULL,
  CHECK (valid_until IS NULL OR valid_until >= valid_from)
);

-- ponti per versione di attività (figli tecnici: eliminati con la bozza)
CREATE TABLE activity_data_categories (
  activity_version_id uuid NOT NULL REFERENCES activity_versions(id) ON DELETE CASCADE,
  category_code       text NOT NULL REFERENCES cat_data_category(code),
  PRIMARY KEY (activity_version_id, category_code)
);
CREATE TABLE activity_subject_categories (
  activity_version_id uuid NOT NULL REFERENCES activity_versions(id) ON DELETE CASCADE,
  category_code       text NOT NULL REFERENCES cat_subject_category(code),
  PRIMARY KEY (activity_version_id, category_code)
);
CREATE TABLE activity_recipient_categories (
  activity_version_id uuid NOT NULL REFERENCES activity_versions(id) ON DELETE CASCADE,
  category_code       text NOT NULL REFERENCES cat_recipient_category(code),
  PRIMARY KEY (activity_version_id, category_code)
);
CREATE TABLE activity_systems (
  activity_version_id uuid NOT NULL REFERENCES activity_versions(id) ON DELETE CASCADE,
  system_id           uuid NOT NULL REFERENCES systems(id) ON DELETE RESTRICT,
  PRIMARY KEY (activity_version_id, system_id)
);
CREATE TABLE activity_measures (
  activity_version_id          uuid NOT NULL REFERENCES activity_versions(id) ON DELETE CASCADE,
  measure_id                   uuid NOT NULL REFERENCES security_measures(id) ON DELETE RESTRICT,
  measure_description_snapshot nonblank NOT NULL,
  PRIMARY KEY (activity_version_id, measure_id)
);
CREATE TABLE activity_engagements (
  activity_version_id uuid NOT NULL REFERENCES activity_versions(id) ON DELETE CASCADE,
  engagement_id       uuid NOT NULL REFERENCES service_engagements(id) ON DELETE RESTRICT,
  role_snapshot       nonblank NOT NULL,
  PRIMARY KEY (activity_version_id, engagement_id)
);

CREATE TABLE transfers (
  LIKE _base INCLUDING ALL,
  activity_version_id    uuid NOT NULL REFERENCES activity_versions(id) ON DELETE CASCADE,
  engagement_id          uuid REFERENCES service_engagements(id) ON DELETE RESTRICT,
  destination_country_code text CHECK (destination_country_code IS NULL OR destination_country_code ~ '^[A-Z]{2}$'),
  international_organization text,
  mechanism_code         text NOT NULL DEFAULT 'to_assess' REFERENCES cat_transfer_mechanism(code),
  assessment             nonblank NOT NULL,
  safeguard_file_id      uuid REFERENCES files(id),
  review_due_on          date NOT NULL,
  CHECK (destination_country_code IS NOT NULL OR (international_organization IS NOT NULL AND btrim(international_organization) <> ''))
);

CREATE TABLE compliance_assessments (
  LIKE _base INCLUDING ALL,
  practice_id         uuid NOT NULL REFERENCES practices(id) ON DELETE RESTRICT,
  activity_version_id uuid REFERENCES activity_versions(id) ON DELETE RESTRICT,
  type_code           text NOT NULL REFERENCES cat_assessment_type(code),
  outcome_code        text NOT NULL REFERENCES cat_assessment_outcome(code),
  reason              nonblank NOT NULL,
  assessed_by         uuid NOT NULL REFERENCES users(id),
  assessed_at         timestamptz NOT NULL,
  review_due_on       date NOT NULL,
  evidence_file_id    uuid REFERENCES files(id)
);

-- ---------------------------------------------------------------- conservazione
CREATE TABLE retention_policy_versions (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  practice_id      uuid NOT NULL REFERENCES practices(id) ON DELETE RESTRICT,
  policy_key       uuid NOT NULL DEFAULT gen_random_uuid(),
  version_number   integer NOT NULL DEFAULT 1 CHECK (version_number > 0),
  record_class_code text NOT NULL REFERENCES cat_record_class(code),
  trigger_code     text NOT NULL REFERENCES cat_retention_trigger(code),
  duration_kind_code text NOT NULL REFERENCES cat_duration_kind(code),
  duration_months  integer CHECK (duration_months >= 0),
  duration_days    integer CHECK (duration_days >= 0),
  criterion        text,
  legal_reference  text,
  review_due_on    date NOT NULL,
  approved_by      uuid REFERENCES users(id),
  approved_at      timestamptz,
  status_code      text NOT NULL DEFAULT 'draft' REFERENCES cat_retention_status(code),
  CHECK ((approved_by IS NULL) = (approved_at IS NULL)),
  CHECK (status_code = 'draft' OR approved_by IS NOT NULL),
  -- «fissa» richiede una durata; «illimitata» e «a criterio» richiedono un criterio motivato.
  -- Una durata NULL non significa mai conservazione illimitata.
  CHECK (duration_kind_code <> 'fixed' OR (duration_months IS NOT NULL OR duration_days IS NOT NULL)),
  CHECK (duration_kind_code = 'fixed' OR (criterion IS NOT NULL AND btrim(criterion) <> '')),
  CHECK (duration_kind_code <> 'indefinite' OR (duration_months IS NULL AND duration_days IS NULL)),
  UNIQUE (policy_key, version_number)
);

CREATE TABLE purpose_retention_rules (
  purpose_version_id uuid NOT NULL REFERENCES purpose_versions(id) ON DELETE CASCADE,
  data_category_code text NOT NULL REFERENCES cat_data_category(code),
  policy_version_id  uuid NOT NULL REFERENCES retention_policy_versions(id) ON DELETE RESTRICT,
  PRIMARY KEY (purpose_version_id, data_category_code)
);

CREATE TABLE disposal_records (
  LIKE _base INCLUDING ALL,
  performed_at        timestamptz NOT NULL DEFAULT now(),
  performed_by        uuid NOT NULL REFERENCES users(id),
  record_class_code   text NOT NULL REFERENCES cat_record_class(code),
  generic_description nonblank NOT NULL,   -- non deve ricreare i dati eliminati
  item_count          integer NOT NULL CHECK (item_count >= 0),
  policy_version_id   uuid REFERENCES retention_policy_versions(id) ON DELETE RESTRICT,
  reason              text
);

-- ---------------------------------------------------------------- documenti, consegne
CREATE TABLE documents (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  practice_id uuid NOT NULL REFERENCES practices(id) ON DELETE RESTRICT,
  type_code   text NOT NULL REFERENCES cat_document_type(code),
  title       nonblank NOT NULL,
  status_code text NOT NULL DEFAULT 'active' REFERENCES cat_record_status(code)
);

CREATE TABLE document_versions (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  document_id    uuid NOT NULL REFERENCES documents(id) ON DELETE RESTRICT,
  version_number integer NOT NULL CHECK (version_number > 0),
  language_code  text NOT NULL REFERENCES cat_language(code),
  body           text NOT NULL DEFAULT '',        -- testo definitivo, dati dello studio già risolti alla pubblicazione
  status_code    text NOT NULL DEFAULT 'draft' REFERENCES cat_document_status(code),
  effective_from date,
  approved_by    uuid REFERENCES users(id),
  approved_at    timestamptz,
  content_hmac   bytea CHECK (content_hmac IS NULL OR length(content_hmac) = 32),
  CHECK ((approved_by IS NULL) = (approved_at IS NULL)),
  UNIQUE (document_id, version_number, language_code)
);

CREATE TABLE notice_activity_versions (
  document_version_id uuid NOT NULL REFERENCES document_versions(id) ON DELETE CASCADE,
  activity_version_id uuid NOT NULL REFERENCES activity_versions(id) ON DELETE RESTRICT,
  PRIMARY KEY (document_version_id, activity_version_id)
);

CREATE TABLE notice_deliveries (
  LIKE _base INCLUDING ALL,
  document_version_id uuid NOT NULL REFERENCES document_versions(id) ON DELETE RESTRICT,
  subject_id          uuid NOT NULL REFERENCES data_subjects(id) ON DELETE RESTRICT,
  representation_id   uuid,
  delivery_code       text NOT NULL UNIQUE DEFAULT 'PS-' || upper(substr(replace(gen_random_uuid()::text, '-', ''), 1, 12)),
  delivered_at        timestamptz,
  channel_code        text NOT NULL REFERENCES cat_delivery_channel(code),
  receipt_status_code text NOT NULL REFERENCES cat_receipt_status(code),
  subject_name_snapshot       nonblank NOT NULL,
  signatory_name_snapshot     text,
  signatory_capacity_snapshot text,
  rendered_hmac       bytea CHECK (rendered_hmac IS NULL OR length(rendered_hmac) = 32),
  rendered_file_id    uuid REFERENCES files(id),   -- solo se il titolare attiva la conservazione del PDF
  recorded_by         uuid NOT NULL REFERENCES users(id),
  note                text,
  -- coerenza rappresentanza/paziente: la rappresentanza riguarda lo stesso soggetto
  FOREIGN KEY (representation_id, subject_id) REFERENCES representations (id, subject_id) ON DELETE RESTRICT,
  CHECK ((receipt_status_code = 'failed_attempt') = (delivered_at IS NULL)),
  CHECK (signatory_capacity_snapshot IS NULL OR signatory_name_snapshot IS NOT NULL),
  CHECK (rendered_file_id IS NULL OR rendered_hmac IS NOT NULL)
);

CREATE TABLE signature_evidence (
  LIKE _base INCLUDING ALL,
  delivery_id       uuid NOT NULL REFERENCES notice_deliveries(id) ON DELETE RESTRICT,
  signed_file_id    uuid NOT NULL REFERENCES files(id),
  signature_type_code text NOT NULL REFERENCES cat_signature_type(code),
  signed_at         timestamptz,                    -- data dichiarata, può mancare
  acquired_at       timestamptz NOT NULL DEFAULT now(),
  verified_by       uuid REFERENCES users(id),
  verification_note text
);

-- ---------------------------------------------------------------- richieste dei diritti
CREATE TABLE rights_requests (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  practice_id          uuid NOT NULL REFERENCES practices(id) ON DELETE RESTRICT,
  subject_id           uuid REFERENCES data_subjects(id) ON DELETE RESTRICT,   -- persona cui i dati si riferiscono
  requester_name       text,
  requester_capacity_code text NOT NULL DEFAULT 'data_subject' REFERENCES cat_requester_capacity(code),
  reference_code       nonblank NOT NULL,
  received_at          timestamptz NOT NULL,
  channel_code         text NOT NULL REFERENCES cat_request_channel(code),
  request_text         nonblank NOT NULL,
  status_code          text NOT NULL DEFAULT 'received' REFERENCES cat_request_status(code),
  assignee_id          uuid NOT NULL REFERENCES users(id),
  identity_status_code text NOT NULL DEFAULT 'not_verified' REFERENCES cat_identity_status(code),
  identity_verified_at timestamptz,
  initial_due_on       date NOT NULL,                 -- calcolata dal trigger se omessa
  effective_due_on     date NOT NULL,
  deadline_rule_version text NOT NULL DEFAULT 'it-1182-v1',
  closed_at            timestamptz,
  CHECK (effective_due_on >= initial_due_on),
  CHECK ((status_code IN ('closed','cancelled')) = (closed_at IS NOT NULL)),
  CHECK (subject_id IS NOT NULL OR requester_name IS NOT NULL),
  UNIQUE (practice_id, reference_code)
);

CREATE TABLE request_rights (
  request_id uuid NOT NULL REFERENCES rights_requests(id) ON DELETE RESTRICT,
  right_code text NOT NULL REFERENCES cat_right(code),
  PRIMARY KEY (request_id, right_code)
);

CREATE TABLE request_events (
  LIKE _base INCLUDING ALL,
  request_id      uuid NOT NULL REFERENCES rights_requests(id) ON DELETE RESTRICT,
  event_type_code text NOT NULL REFERENCES cat_request_event_type(code),
  occurred_at     timestamptz NOT NULL DEFAULT now(),
  actor_id        uuid NOT NULL REFERENCES users(id),
  description     nonblank NOT NULL,
  file_id         uuid REFERENCES files(id)
);

CREATE TABLE request_extensions (
  LIKE _base INCLUDING ALL,
  request_id           uuid NOT NULL REFERENCES rights_requests(id) ON DELETE RESTRICT,
  new_due_on           date NOT NULL,
  reason               nonblank NOT NULL,
  decided_at           timestamptz NOT NULL,
  notified_at          timestamptz,
  notification_file_id uuid REFERENCES files(id)
);

CREATE TABLE request_decisions (
  LIKE _base INCLUDING ALL,
  request_id       uuid NOT NULL,
  right_code       text NOT NULL,
  outcome_code     text NOT NULL REFERENCES cat_request_outcome(code),
  reason           text,
  decided_by       uuid NOT NULL REFERENCES users(id),
  decided_at       timestamptz NOT NULL,
  response_file_id uuid REFERENCES files(id),
  sent_at          timestamptz,
  FOREIGN KEY (request_id, right_code) REFERENCES request_rights (request_id, right_code) ON DELETE RESTRICT,
  CHECK (outcome_code = 'granted' OR (reason IS NOT NULL AND btrim(reason) <> '')),
  UNIQUE (request_id, right_code)
);

-- ---------------------------------------------------------------- incidenti
CREATE TABLE incidents (
  LIKE _base INCLUDING ALL,
  LIKE _mut  INCLUDING ALL,
  practice_id    uuid NOT NULL REFERENCES practices(id) ON DELETE RESTRICT,
  reference_code nonblank NOT NULL,
  discovered_at  timestamptz NOT NULL,
  awareness_at   timestamptz,
  notification_deadline_at timestamptz,               -- = awareness_at + 72 ore (istanti), calcolata dal trigger
  description    nonblank NOT NULL,
  classification_code text NOT NULL DEFAULT 'to_assess' REFERENCES cat_incident_classification(code),
  status_code    text NOT NULL DEFAULT 'reported' REFERENCES cat_incident_status(code),
  assignee_id    uuid NOT NULL REFERENCES users(id),
  closed_at      timestamptz,
  CHECK ((status_code = 'closed') = (closed_at IS NOT NULL)),
  UNIQUE (practice_id, reference_code)
);

CREATE TABLE incident_assessments (
  LIKE _base INCLUDING ALL,
  incident_id  uuid NOT NULL REFERENCES incidents(id) ON DELETE RESTRICT,
  assessed_at  timestamptz NOT NULL,
  assessed_by  uuid NOT NULL REFERENCES users(id),
  risk_level_code text NOT NULL REFERENCES cat_risk_level(code),
  reason       nonblank NOT NULL,
  approx_subject_count bigint CHECK (approx_subject_count >= 0),
  approx_record_count  bigint CHECK (approx_record_count >= 0),
  consequences nonblank NOT NULL,
  notification_decision_code       text NOT NULL REFERENCES cat_notification_decision(code),
  subject_communication_decision_code text NOT NULL REFERENCES cat_subject_comm_decision(code),
  delay_reason text
);
CREATE TABLE incident_data_categories (
  assessment_id uuid NOT NULL REFERENCES incident_assessments(id) ON DELETE RESTRICT,
  category_code text NOT NULL REFERENCES cat_data_category(code),
  PRIMARY KEY (assessment_id, category_code)
);
CREATE TABLE incident_systems (
  assessment_id uuid NOT NULL REFERENCES incident_assessments(id) ON DELETE RESTRICT,
  system_id     uuid NOT NULL REFERENCES systems(id) ON DELETE RESTRICT,
  PRIMARY KEY (assessment_id, system_id)
);

CREATE TABLE incident_actions (
  LIKE _base INCLUDING ALL,
  incident_id      uuid NOT NULL REFERENCES incidents(id) ON DELETE RESTRICT,
  type_code        text NOT NULL REFERENCES cat_incident_action_type(code),
  occurred_at      timestamptz NOT NULL,
  description      nonblank NOT NULL,
  actor_id         uuid NOT NULL REFERENCES users(id),
  protocol_reference text,                         -- protocollo/ricevuta, solo dopo l'azione effettiva
  evidence_file_id uuid REFERENCES files(id),
  CHECK (type_code <> 'notification' OR (protocol_reference IS NOT NULL AND btrim(protocol_reference) <> ''))
);

-- ---------------------------------------------------------------- audit, backup, varie
CREATE SEQUENCE audit_seq;
CREATE TABLE audit_events (
  id             bigint PRIMARY KEY,               -- assegnato dal trigger, nell'ordine della catena
  occurred_at    timestamptz NOT NULL DEFAULT now(),
  actor_id       uuid REFERENCES users(id),
  action_code    nonblank NOT NULL,
  outcome_code   text NOT NULL CHECK (outcome_code IN ('success','failure','denied')),
  correlation_id uuid NOT NULL,
  target_type    text,
  target_id      uuid,                             -- volutamente senza FK: sopravvive alle cancellazioni
  reason_code    text,
  prev_hash      bytea,
  row_hash       bytea
);

CREATE TABLE backup_runs (
  LIKE _base INCLUDING ALL,
  started_at     timestamptz NOT NULL,
  finished_at    timestamptz,
  status_code    text NOT NULL REFERENCES cat_backup_status(code),
  artifact_reference text,
  key_version    text,
  verified_at    timestamptz,
  error_code     text
);
CREATE TABLE restore_tests (
  LIKE _base INCLUDING ALL,
  backup_id    uuid NOT NULL REFERENCES backup_runs(id) ON DELETE RESTRICT,
  started_at   timestamptz NOT NULL,
  finished_at  timestamptz,
  outcome_code text NOT NULL REFERENCES cat_restore_outcome(code),
  report       nonblank NOT NULL,
  performed_by uuid NOT NULL REFERENCES users(id)
);

CREATE TABLE schema_migrations (
  version             text PRIMARY KEY,
  checksum            text NOT NULL,
  applied_at          timestamptz NOT NULL DEFAULT now(),
  application_version text NOT NULL
);

CREATE TABLE processed_commands (          -- idempotenza dei comandi ritentati
  command_id   uuid PRIMARY KEY,
  command_type nonblank NOT NULL,
  created_at   timestamptz NOT NULL DEFAULT now(),
  result_id    uuid
);

CREATE TABLE local_holidays (              -- festività locali (patrono) per il calcolo dei termini
  day   date PRIMARY KEY,
  label nonblank NOT NULL
);

-- ---------------------------------------------------------------- FK created_by e indici
DO $$
DECLARE r record;
BEGIN
  FOR r IN SELECT table_name FROM information_schema.columns
           WHERE table_schema = 'ps' AND column_name = 'created_by' AND table_name NOT LIKE '\_%'
  LOOP
    EXECUTE format('ALTER TABLE %I ADD CONSTRAINT %I FOREIGN KEY (created_by) REFERENCES users(id)',
                   r.table_name, r.table_name || '_created_by_fk');
  END LOOP;
END $$;

-- indice su ogni FK usata nelle verifiche di dipendenza (esclusi i cataloghi, minuscoli)
DO $$
DECLARE r record;
BEGIN
  FOR r IN
    SELECT c.conrelid::regclass AS tbl, c.conname,
           (SELECT string_agg(quote_ident(a.attname), ',' ORDER BY k.ord)
              FROM unnest(c.conkey) WITH ORDINALITY k(attnum, ord)
              JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum) AS cols
    FROM pg_constraint c
    JOIN pg_namespace n ON n.oid = c.connamespace
    WHERE c.contype = 'f' AND n.nspname = 'ps'
      AND NOT EXISTS (SELECT 1 FROM pg_class cl WHERE cl.oid = c.confrelid AND cl.relname LIKE 'cat\_%')
      AND NOT EXISTS (SELECT 1 FROM pg_index i
                       WHERE i.indrelid = c.conrelid
                         AND (i.indkey::int2[])[0:array_length(c.conkey,1)-1] = c.conkey)
  LOOP
    EXECUTE format('CREATE INDEX %I ON %s (%s)', left('ix_' || r.conname, 63), r.tbl, r.cols);
  END LOOP;
END $$;

CREATE INDEX ix_requests_open_due      ON rights_requests (effective_due_on) WHERE closed_at IS NULL;
CREATE INDEX ix_activity_review        ON activity_versions (review_due_on) WHERE status_code = 'published';
CREATE INDEX ix_deliveries_subject     ON notice_deliveries (subject_id, delivered_at);
CREATE INDEX ix_incidents_open         ON incidents (notification_deadline_at) WHERE closed_at IS NULL;
CREATE INDEX ix_audit_time             ON audit_events (occurred_at);
CREATE INDEX ix_audit_actor            ON audit_events (actor_id, occurred_at);
CREATE INDEX ix_retention_review       ON retention_policy_versions (review_due_on) WHERE status_code = 'approved';

DROP TABLE _base, _mut;

COMMIT;
