-- 003_rules_and_grants.sql — invarianti applicate dal database, funzioni, privilegi.
-- Eseguire come privacy_migrator dopo 001 e 002.
-- Principio: il database garantisce le invarianti STRUTTURALI (immutabilità, coerenza, completezza alla
-- pubblicazione). Le regole giuridiche restano configurabili e motivate nei dati.
BEGIN;
SET LOCAL search_path = ps, pg_catalog;

-- ================================================================ utilità generali
CREATE FUNCTION touch_row() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  NEW.updated_at  := now();
  NEW.row_version := OLD.row_version + 1;   -- l'app aggiorna con WHERE id=$1 AND row_version=$2
  RETURN NEW;
END $$;

CREATE FUNCTION forbid_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'la tabella % è append-only: % non ammesso', TG_TABLE_NAME, TG_OP
    USING ERRCODE = 'restrict_violation';
END $$;

DO $$
DECLARE r record; t text;
BEGIN
  FOR r IN SELECT table_name FROM information_schema.columns
           WHERE table_schema = 'ps' AND column_name = 'row_version'
  LOOP
    EXECUTE format('CREATE TRIGGER trg_touch BEFORE UPDATE ON %I FOR EACH ROW EXECUTE FUNCTION touch_row()', r.table_name);
  END LOOP;
  -- tabelle di soli eventi: nessuna modifica, nessuna cancellazione
  FOREACH t IN ARRAY ARRAY['audit_events','request_events','incident_actions','signature_evidence','disposal_records','request_extensions']
  LOOP
    EXECUTE format('CREATE TRIGGER trg_append_only BEFORE UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION forbid_mutation()', t);
    EXECUTE format('CREATE TRIGGER trg_no_truncate BEFORE TRUNCATE ON %I FOR EACH STATEMENT EXECUTE FUNCTION forbid_mutation()', t);
  END LOOP;
END $$;

-- ================================================================ calcolo dei termini (regola it-1182-v1)
-- Regola PROPOSTA, da confermare dal consulente sul Reg. 1182/71 prima del rilascio:
--   scadenza = giorno corrispondente del mese successivo alla ricezione (n mesi), con il giorno ricondotto
--   all'ultimo del mese se manca; se cade di sabato, domenica o festività, slitta al primo giorno lavorativo.
CREATE FUNCTION easter_monday(p_year int) RETURNS date LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE a int; b int; c int; d int; e int; f int; g int; h int; i int; k int; l int; m int; mon int; dd int;
BEGIN
  a := p_year % 19;  b := p_year / 100;  c := p_year % 100;
  d := b / 4;        e := b % 4;         f := (b + 8) / 25;
  g := (b - f + 1) / 3;
  h := (19 * a + b - d - g + 15) % 30;
  i := c / 4;        k := c % 4;
  l := (32 + 2 * e + 2 * i - h - k) % 7;
  m := (a + 11 * h + 22 * l) / 451;
  mon := (h + l - 7 * m + 114) / 31;
  dd  := ((h + l - 7 * m + 114) % 31) + 1;
  RETURN make_date(p_year, mon, dd) + 1;
END $$;

CREATE FUNCTION is_non_working(p_day date) RETURNS boolean LANGUAGE sql STABLE AS $$
  SELECT extract(isodow FROM p_day) IN (6, 7)
      OR (extract(month FROM p_day)::int, extract(day FROM p_day)::int) IN
         ((1,1),(1,6),(4,25),(5,1),(6,2),(8,15),(11,1),(12,8),(12,25),(12,26))
      OR p_day = easter_monday(extract(year FROM p_day)::int)
      OR EXISTS (SELECT 1 FROM local_holidays h WHERE h.day = p_day)
$$;

CREATE FUNCTION compute_due_date(p_received date, p_months int DEFAULT 1) RETURNS date
LANGUAGE plpgsql STABLE AS $$
DECLARE d date;
BEGIN
  d := (p_received + make_interval(months => p_months))::date;   -- fine mese ricondotta automaticamente
  WHILE is_non_working(d) LOOP d := d + 1; END LOOP;
  RETURN d;
END $$;

-- richieste: calcolo iniziale, termini protetti, chiusura solo con riscontro
CREATE FUNCTION rights_request_defaults() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE rd date;
BEGIN
  IF TG_OP = 'INSERT' THEN
    rd := (NEW.received_at AT TIME ZONE 'Europe/Rome')::date;
    IF NEW.initial_due_on IS NULL THEN NEW.initial_due_on := compute_due_date(rd, 1); END IF;
    IF NEW.effective_due_on IS NULL THEN NEW.effective_due_on := NEW.initial_due_on; END IF;
  END IF;
  RETURN NEW;
END $$;

CREATE FUNCTION guard_rights_request() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE n int;
BEGIN
  IF current_user = 'privacy_migrator' THEN RETURN NEW; END IF;
  IF NEW.initial_due_on <> OLD.initial_due_on OR NEW.received_at <> OLD.received_at THEN
    RAISE EXCEPTION 'data di ricezione e termine iniziale non modificabili';
  END IF;
  -- il termine effettivo cambia solo tramite una proroga registrata (trigger annidato)
  IF NEW.effective_due_on <> OLD.effective_due_on AND pg_trigger_depth() < 2 THEN
    RAISE EXCEPTION 'il termine si modifica solo registrando una proroga motivata';
  END IF;
  IF OLD.status_code IN ('closed','cancelled') AND NEW.status_code <> OLD.status_code THEN
    RAISE EXCEPTION 'richiesta già %: stato non modificabile', OLD.status_code;
  END IF;
  IF NEW.status_code = 'closed' AND OLD.status_code <> 'closed' THEN
    SELECT count(*) INTO n FROM request_rights WHERE request_id = NEW.id;
    IF n = 0 THEN RAISE EXCEPTION 'chiusura impossibile: nessun diritto classificato'; END IF;
    SELECT count(*) INTO n FROM request_rights rr
      WHERE rr.request_id = NEW.id
        AND NOT EXISTS (SELECT 1 FROM request_decisions d
                         WHERE d.request_id = rr.request_id AND d.right_code = rr.right_code
                           AND d.sent_at IS NOT NULL);
    IF n > 0 THEN RAISE EXCEPTION 'chiusura impossibile: % diritto/i senza decisione e riscontro inviato', n; END IF;
  END IF;
  IF NEW.status_code = 'cancelled' AND OLD.status_code <> 'cancelled' THEN
    IF NOT EXISTS (SELECT 1 FROM request_events WHERE request_id = NEW.id AND event_type_code = 'cancelled') THEN
      RAISE EXCEPTION 'annullamento impossibile senza un evento motivato di annullamento';
    END IF;
  END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER trg_rr_defaults BEFORE INSERT ON rights_requests FOR EACH ROW EXECUTE FUNCTION rights_request_defaults();
CREATE TRIGGER trg_rr_guard    BEFORE UPDATE ON rights_requests FOR EACH ROW EXECUTE FUNCTION guard_rights_request();

CREATE FUNCTION request_extension_apply() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE rr rights_requests; max_due date;
BEGIN
  SELECT * INTO rr FROM rights_requests WHERE id = NEW.request_id FOR UPDATE;
  IF rr.closed_at IS NOT NULL THEN RAISE EXCEPTION 'proroga su richiesta chiusa'; END IF;
  -- limite: un mese + due mesi ulteriori = tre mesi complessivi dalla ricezione (da confermare, v. documento 3)
  max_due := compute_due_date((rr.received_at AT TIME ZONE 'Europe/Rome')::date, 3);
  IF NEW.new_due_on <= rr.effective_due_on THEN RAISE EXCEPTION 'la proroga deve spostare il termine in avanti'; END IF;
  IF NEW.new_due_on > max_due THEN RAISE EXCEPTION 'proroga oltre il limite complessivo (%)', max_due; END IF;
  RETURN NEW;
END $$;
CREATE FUNCTION request_extension_after() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  UPDATE rights_requests SET effective_due_on = NEW.new_due_on WHERE id = NEW.request_id;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_ext_check BEFORE INSERT ON request_extensions FOR EACH ROW EXECUTE FUNCTION request_extension_apply();
CREATE TRIGGER trg_ext_apply AFTER  INSERT ON request_extensions FOR EACH ROW EXECUTE FUNCTION request_extension_after();

-- incidenti: scadenza di notifica in ISTANTI (72 ore), mai in giorni lavorativi
CREATE FUNCTION incident_deadline() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  NEW.notification_deadline_at := CASE WHEN NEW.awareness_at IS NULL THEN NULL
                                       ELSE NEW.awareness_at + interval '72 hours' END;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_incident_deadline BEFORE INSERT OR UPDATE ON incidents FOR EACH ROW EXECUTE FUNCTION incident_deadline();

-- ================================================================ registro: immutabilità e pubblicazione
-- Figli di una versione: modificabili solo finché la versione è in bozza/in verifica.
CREATE FUNCTION guard_version_children() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE col text := TG_ARGV[0]; parent text := TG_ARGV[1]; editable text[] := string_to_array(TG_ARGV[2], ',');
        vid uuid; st text; rec jsonb;
BEGIN
  rec := CASE WHEN TG_OP = 'DELETE' THEN to_jsonb(OLD) ELSE to_jsonb(NEW) END;
  vid := (rec ->> col)::uuid;
  EXECUTE format('SELECT status_code FROM %I WHERE id = $1', parent) INTO st USING vid;
  IF st IS NOT NULL AND NOT (st = ANY (editable)) THEN
    RAISE EXCEPTION '% non modificabile: la versione di % è in stato %', TG_TABLE_NAME, parent, st
      USING ERRCODE = 'restrict_violation';
  END IF;
  RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
END $$;

CREATE FUNCTION guard_purpose_children() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE pid uuid; st text; rec jsonb;
BEGIN
  rec := CASE WHEN TG_OP = 'DELETE' THEN to_jsonb(OLD) ELSE to_jsonb(NEW) END;
  pid := (rec ->> 'purpose_version_id')::uuid;
  SELECT av.status_code INTO st FROM purpose_versions pv JOIN activity_versions av ON av.id = pv.activity_version_id WHERE pv.id = pid;
  IF st IS NOT NULL AND st NOT IN ('draft','in_review') THEN
    RAISE EXCEPTION '% non modificabile: la versione dell''attività è in stato %', TG_TABLE_NAME, st
      USING ERRCODE = 'restrict_violation';
  END IF;
  RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
END $$;

DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['purpose_versions','activity_data_categories','activity_subject_categories',
       'activity_recipient_categories','activity_systems','activity_measures','activity_engagements','transfers']
  LOOP
    EXECUTE format('CREATE TRIGGER trg_guard_children BEFORE INSERT OR UPDATE OR DELETE ON %I
                    FOR EACH ROW EXECUTE FUNCTION guard_version_children(''activity_version_id'',''activity_versions'',''draft,in_review'')', t);
  END LOOP;
  FOREACH t IN ARRAY ARRAY['purpose_legal_grounds','purpose_retention_rules']
  LOOP
    EXECUTE format('CREATE TRIGGER trg_guard_children BEFORE INSERT OR UPDATE OR DELETE ON %I
                    FOR EACH ROW EXECUTE FUNCTION guard_purpose_children()', t);
  END LOOP;
END $$;
CREATE TRIGGER trg_guard_children BEFORE INSERT OR UPDATE OR DELETE ON notice_activity_versions
  FOR EACH ROW EXECUTE FUNCTION guard_version_children('document_version_id','document_versions','draft,approved');

-- Completezza richiesta alla pubblicazione di una versione di attività (art. 30)
CREATE FUNCTION validate_activity_publish(v activity_versions) RETURNS void LANGUAGE plpgsql STABLE AS $$
DECLARE errs text[] := '{}'; n int;
BEGIN
  IF v.description IS NULL OR btrim(v.description) = '' THEN errs := array_append(errs, 'descrizione mancante'); END IF;
  IF v.approved_by IS NULL THEN errs := array_append(errs, 'approvazione mancante'); END IF;
  IF v.review_due_on IS NULL THEN errs := array_append(errs, 'data di riesame mancante'); END IF;

  SELECT count(*) INTO n FROM purpose_versions WHERE activity_version_id = v.id;
  IF n = 0 THEN errs := array_append(errs, 'nessuna finalità'); END IF;
  SELECT count(*) INTO n FROM purpose_versions pv
    WHERE pv.activity_version_id = v.id
      AND (pv.legal_assessment IS NULL OR btrim(pv.legal_assessment) = ''
           OR pv.description IS NULL OR btrim(pv.description) = ''
           OR pv.consent_assessment_code = 'to_assess'
           OR NOT EXISTS (SELECT 1 FROM purpose_legal_grounds g WHERE g.purpose_version_id = pv.id));
  IF n > 0 THEN errs := array_append(errs, format('%s finalità senza descrizione, valutazione, presupposto o con consenso da valutare', n)); END IF;

  IF NOT EXISTS (SELECT 1 FROM activity_data_categories WHERE activity_version_id = v.id) THEN errs := array_append(errs, 'nessuna categoria di dati'); END IF;
  IF NOT EXISTS (SELECT 1 FROM activity_subject_categories WHERE activity_version_id = v.id) THEN errs := array_append(errs, 'nessuna categoria di interessati'); END IF;

  IF EXISTS (SELECT 1 FROM activity_data_categories a JOIN cat_data_category c ON c.code = a.category_code
              WHERE a.activity_version_id = v.id AND c.classification_code = 'to_assess') THEN
    errs := array_append(errs, 'categoria di dati «da valutare»');
  END IF;
  IF EXISTS (SELECT 1 FROM activity_data_categories a JOIN cat_data_category c ON c.code = a.category_code
              WHERE a.activity_version_id = v.id AND c.classification_code = 'art9')
     AND NOT EXISTS (SELECT 1 FROM purpose_legal_grounds g JOIN purpose_versions pv ON pv.id = g.purpose_version_id
                      WHERE pv.activity_version_id = v.id AND g.article9_code IS NOT NULL) THEN
    errs := array_append(errs, 'dati art. 9 senza condizione dell''art. 9');
  END IF;
  IF EXISTS (SELECT 1 FROM activity_data_categories a JOIN cat_data_category c ON c.code = a.category_code
              WHERE a.activity_version_id = v.id AND c.classification_code = 'art10')
     AND NOT EXISTS (SELECT 1 FROM purpose_legal_grounds g JOIN purpose_versions pv ON pv.id = g.purpose_version_id
                      WHERE pv.activity_version_id = v.id AND g.national_reference IS NOT NULL AND btrim(g.national_reference) <> '') THEN
    errs := array_append(errs, 'dati art. 10 senza disposizione applicabile');
  END IF;
  IF cardinality(errs) > 0 THEN
    RAISE EXCEPTION 'pubblicazione rifiutata: %', array_to_string(errs, '; ') USING ERRCODE = 'check_violation';
  END IF;
END $$;

CREATE FUNCTION guard_activity_version() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    IF OLD.status_code <> 'draft' THEN
      RAISE EXCEPTION 'solo le bozze si eliminano' USING ERRCODE = 'restrict_violation';
    END IF;
    RETURN OLD;
  END IF;
  IF OLD.status_code = 'superseded' THEN
    RAISE EXCEPTION 'versione superata immutabile' USING ERRCODE = 'restrict_violation';
  END IF;
  IF OLD.status_code = 'published' THEN
    IF NEW.status_code NOT IN ('published','superseded') THEN
      RAISE EXCEPTION 'una versione pubblicata può solo diventare «superata»' USING ERRCODE = 'restrict_violation';
    END IF;
    IF (NEW.activity_id, NEW.version_number, NEW.description, NEW.effective_from, NEW.review_due_on,
        NEW.approved_by, NEW.approved_at, NEW.change_reason)
       IS DISTINCT FROM
       (OLD.activity_id, OLD.version_number, OLD.description, OLD.effective_from, OLD.review_due_on,
        OLD.approved_by, OLD.approved_at, OLD.change_reason) THEN
      RAISE EXCEPTION 'contenuto della versione pubblicata immutabile' USING ERRCODE = 'restrict_violation';
    END IF;
    RETURN NEW;
  END IF;
  IF NEW.status_code = 'published' AND OLD.status_code <> 'published' THEN
    IF OLD.status_code <> 'in_review' THEN
      RAISE EXCEPTION 'si pubblica solo una versione «in verifica»';
    END IF;
    PERFORM validate_activity_publish(NEW);
  END IF;
  IF NEW.status_code = 'superseded' THEN
    RAISE EXCEPTION 'una versione non pubblicata non può diventare «superata»';
  END IF;
  RETURN NEW;
END $$;
-- Una versione nasce sempre in bozza: altrimenti si aggirerebbe la validazione di pubblicazione.
CREATE FUNCTION require_draft_on_insert() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF NEW.status_code <> 'draft' AND current_user <> 'privacy_migrator' THEN
    RAISE EXCEPTION 'una nuova versione nasce in bozza (stato richiesto: %)', NEW.status_code USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_av_insert BEFORE INSERT ON activity_versions FOR EACH ROW EXECUTE FUNCTION require_draft_on_insert();
CREATE TRIGGER trg_activity_version BEFORE UPDATE OR DELETE ON activity_versions
  FOR EACH ROW EXECUTE FUNCTION guard_activity_version();

-- ================================================================ documenti: versioni e consegne
CREATE FUNCTION guard_document_version() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE tp text; n int;
BEGIN
  IF TG_OP = 'DELETE' THEN
    IF OLD.status_code <> 'draft' THEN RAISE EXCEPTION 'solo le bozze si eliminano' USING ERRCODE = 'restrict_violation'; END IF;
    RETURN OLD;
  END IF;
  -- contenuto bloccato dopo l'approvazione, salvo il ritorno a bozza
  IF OLD.status_code <> 'draft' AND NEW.status_code <> 'draft' THEN
    IF (NEW.document_id, NEW.version_number, NEW.language_code, NEW.body, NEW.approved_by, NEW.approved_at, NEW.content_hmac)
       IS DISTINCT FROM
       (OLD.document_id, OLD.version_number, OLD.language_code, OLD.body, OLD.approved_by, OLD.approved_at, OLD.content_hmac) THEN
      RAISE EXCEPTION 'contenuto della versione % immutabile', OLD.status_code USING ERRCODE = 'restrict_violation';
    END IF;
  END IF;
  IF OLD.status_code = 'withdrawn' AND NEW.status_code <> 'withdrawn' THEN RAISE EXCEPTION 'versione ritirata'; END IF;
  IF OLD.status_code = 'published' AND NEW.status_code NOT IN ('published','withdrawn') THEN
    RAISE EXCEPTION 'una versione pubblicata si può solo ritirare';
  END IF;
  IF NEW.status_code = 'approved' AND OLD.status_code = 'draft' THEN
    IF NEW.approved_by IS NULL THEN RAISE EXCEPTION 'approvazione mancante'; END IF;
    IF NEW.content_hmac IS NULL THEN RAISE EXCEPTION 'impronta del contenuto mancante'; END IF;
    IF btrim(NEW.body) = '' THEN RAISE EXCEPTION 'testo vuoto'; END IF;
    IF NEW.body ~ '\{\{' THEN RAISE EXCEPTION 'segnaposto non risolti nel testo'; END IF;
  END IF;
  IF NEW.status_code = 'published' AND OLD.status_code <> 'published' THEN
    IF OLD.status_code <> 'approved' THEN RAISE EXCEPTION 'si pubblica solo una versione approvata'; END IF;
    SELECT type_code INTO tp FROM documents WHERE id = NEW.document_id;
    IF tp = 'notice' THEN
      SELECT count(*) INTO n FROM notice_activity_versions nav WHERE nav.document_version_id = NEW.id;
      IF n = 0 THEN RAISE EXCEPTION 'informativa senza collegamento al registro'; END IF;
      SELECT count(*) INTO n FROM notice_activity_versions nav JOIN activity_versions av ON av.id = nav.activity_version_id
        WHERE nav.document_version_id = NEW.id AND av.status_code <> 'published';
      IF n > 0 THEN RAISE EXCEPTION 'informativa collegata a % versioni di attività non pubblicate', n; END IF;
    END IF;
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_dv_insert BEFORE INSERT ON document_versions FOR EACH ROW EXECUTE FUNCTION require_draft_on_insert();
CREATE TRIGGER trg_document_version BEFORE UPDATE OR DELETE ON document_versions
  FOR EACH ROW EXECUTE FUNCTION guard_document_version();

-- Consegna: solo di versioni pubblicate di informative; rappresentanza verificata e valida alla data.
CREATE FUNCTION guard_delivery_insert() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE vs text; tp text; r representations; d date;
BEGIN
  SELECT dv.status_code, doc.type_code INTO vs, tp
    FROM document_versions dv JOIN documents doc ON doc.id = dv.document_id WHERE dv.id = NEW.document_version_id;
  IF vs IS DISTINCT FROM 'published' THEN RAISE EXCEPTION 'la versione non è pubblicata (stato: %)', vs; END IF;
  IF tp <> 'notice' THEN RAISE EXCEPTION 'le consegne riguardano le informative'; END IF;
  IF NEW.representation_id IS NOT NULL THEN
    SELECT * INTO r FROM representations WHERE id = NEW.representation_id;
    d := (COALESCE(NEW.delivered_at, now()) AT TIME ZONE 'Europe/Rome')::date;
    IF r.verified_at IS NULL THEN RAISE EXCEPTION 'poteri di rappresentanza non verificati'; END IF;
    IF d < r.valid_from OR (r.valid_until IS NOT NULL AND d > r.valid_until) THEN
      RAISE EXCEPTION 'poteri di rappresentanza non validi alla data %', d;
    END IF;
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_delivery_insert BEFORE INSERT ON notice_deliveries FOR EACH ROW EXECUTE FUNCTION guard_delivery_insert();

-- Le consegne sono eventi: nessuna modifica, salvo l'anonimizzazione degli snapshot eseguita dalla
-- procedura remove_subject (SECURITY DEFINER, quindi con current_user = proprietario).
CREATE FUNCTION guard_delivery_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'le consegne non si eliminano' USING ERRCODE = 'restrict_violation';
  END IF;
  IF current_user <> 'privacy_migrator'
     OR (to_jsonb(NEW) - 'subject_name_snapshot' - 'signatory_name_snapshot' - 'signatory_capacity_snapshot' - 'note')
        <> (to_jsonb(OLD) - 'subject_name_snapshot' - 'signatory_name_snapshot' - 'signatory_capacity_snapshot' - 'note') THEN
    RAISE EXCEPTION 'le consegne sono immutabili' USING ERRCODE = 'restrict_violation';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_delivery_mutation BEFORE UPDATE OR DELETE ON notice_deliveries
  FOR EACH ROW EXECUTE FUNCTION guard_delivery_mutation();

-- Interessato rimosso: congelato, salvo la procedura.
CREATE FUNCTION guard_subject() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF OLD.status_code = 'removed' AND current_user <> 'privacy_migrator' THEN
    RAISE EXCEPTION 'interessato rimosso: non modificabile' USING ERRCODE = 'restrict_violation';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_subject_guard BEFORE UPDATE ON data_subjects FOR EACH ROW EXECUTE FUNCTION guard_subject();

-- ================================================================ audit con catena di hash
CREATE FUNCTION audit_row_hash(e audit_events) RETURNS bytea LANGUAGE sql STABLE AS $$
  SELECT sha256(e.prev_hash || convert_to(concat_ws('|',
    e.id::text,
    to_char(e.occurred_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US'),
    coalesce(e.actor_id::text, ''), e.action_code, e.outcome_code, e.correlation_id::text,
    coalesce(e.target_type, ''), coalesce(e.target_id::text, ''), coalesce(e.reason_code, '')), 'UTF8'))
$$;

CREATE FUNCTION audit_chain() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path = ps, pg_catalog AS $$
DECLARE prev bytea;
BEGIN
  PERFORM pg_advisory_xact_lock(hashtext('ps.audit_chain'));   -- serializza: ordine di catena = ordine di id
  SELECT row_hash INTO prev FROM audit_events ORDER BY id DESC LIMIT 1;
  NEW.id          := nextval('audit_seq');
  NEW.occurred_at := clock_timestamp();
  NEW.prev_hash   := COALESCE(prev, decode(repeat('00', 32), 'hex'));
  NEW.row_hash    := audit_row_hash(NEW);
  RETURN NEW;
END $$;
CREATE TRIGGER trg_audit_chain BEFORE INSERT ON audit_events FOR EACH ROW EXECUTE FUNCTION audit_chain();

-- Restituisce l'id della prima riga non coerente, NULL se la catena è integra.
-- Rileva modifiche e cancellazioni intermedie; la cancellazione delle ultime righe non è rilevabile
-- senza un riferimento esterno (ancorare periodicamente l'ultimo row_hash nel backup).
CREATE FUNCTION verify_audit_chain() RETURNS bigint LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = ps, pg_catalog AS $$
DECLARE r audit_events; expected bytea := decode(repeat('00', 32), 'hex');
BEGIN
  FOR r IN SELECT * FROM audit_events ORDER BY id LOOP
    IF r.prev_hash IS DISTINCT FROM expected OR r.row_hash IS DISTINCT FROM audit_row_hash(r) THEN RETURN r.id; END IF;
    expected := r.row_hash;
  END LOOP;
  RETURN NULL;
END $$;

-- ================================================================ eliminazione controllata
CREATE FUNCTION dispose_file(p_file uuid) RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path = ps, pg_catalog AS $$
BEGIN
  UPDATE files SET status_code = 'disposed', ciphertext = NULL, nonce = NULL, auth_tag = NULL,
                   wrapped_key = NULL, key_version = NULL, plaintext_hmac = NULL
   WHERE id = p_file AND status_code <> 'disposed';
END $$;

-- F07: rimozione di un interessato (previa valutazione del titolare sull'applicabilità dell'art. 17
-- e sui vincoli di conservazione). Azzera l'anagrafica, anonimizza gli snapshot delle consegne,
-- svuota i file collegati, registra disposal_record e audit. Le richieste dei diritti e le relazioni di
-- rappresentanza verso altri soggetti NON sono toccate: seguono le proprie regole di conservazione.
CREATE FUNCTION remove_subject(p_subject uuid, p_actor uuid, p_reason text) RETURNS void
LANGUAGE plpgsql SECURITY DEFINER SET search_path = ps, pg_catalog AS $$
DECLARE s data_subjects; n_del int := 0; n_files int := 0; f uuid;
BEGIN
  IF p_reason IS NULL OR btrim(p_reason) = '' THEN RAISE EXCEPTION 'motivazione obbligatoria'; END IF;
  SELECT * INTO s FROM data_subjects WHERE id = p_subject FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'interessato inesistente'; END IF;
  IF s.status_code = 'removed' THEN RAISE EXCEPTION 'interessato già rimosso'; END IF;

  FOR f IN
    SELECT rendered_file_id FROM notice_deliveries WHERE subject_id = p_subject AND rendered_file_id IS NOT NULL
    UNION
    SELECT se.signed_file_id FROM signature_evidence se JOIN notice_deliveries d ON d.id = se.delivery_id WHERE d.subject_id = p_subject
    UNION
    SELECT evidence_file_id FROM representations WHERE subject_id = p_subject AND evidence_file_id IS NOT NULL
  LOOP
    PERFORM dispose_file(f);
    n_files := n_files + 1;
  END LOOP;

  UPDATE notice_deliveries
     SET subject_name_snapshot = 'Interessato rimosso', signatory_name_snapshot = NULL,
         signatory_capacity_snapshot = NULL, note = NULL
   WHERE subject_id = p_subject;
  GET DIAGNOSTICS n_del = ROW_COUNT;

  UPDATE data_subjects
     SET first_name = 'Interessato', last_name = 'rimosso', birth_date = NULL, tax_code = NULL,
         email = NULL, phone = NULL, status_code = 'removed'
   WHERE id = p_subject;

  INSERT INTO disposal_records (performed_by, record_class_code, generic_description, item_count, reason)
    VALUES (p_actor, 'notice_proof', 'Rimozione interessato', n_del + n_files, p_reason);
  INSERT INTO audit_events (actor_id, action_code, outcome_code, correlation_id, target_type, target_id, reason_code)
    VALUES (p_actor, 'subject.remove', 'success', gen_random_uuid(), 'data_subject', p_subject, 'removal');
END $$;

-- ================================================================ search_path fisso per tutte le funzioni
-- Le funzioni risolvono i nomi dello schema ps indipendentemente dal search_path del chiamante.
DO $$
DECLARE r record;
BEGIN
  FOR r IN SELECT p.oid::regprocedure AS sig FROM pg_proc p
           WHERE p.pronamespace = 'ps'::regnamespace AND p.proconfig IS NULL
  LOOP
    EXECUTE format('ALTER FUNCTION %s SET search_path = ps, pg_catalog', r.sig);
  END LOOP;
END $$;

-- ================================================================ privilegi
REVOKE ALL ON SCHEMA ps FROM PUBLIC;
GRANT USAGE ON SCHEMA ps TO privacy_app;
REVOKE ALL ON ALL TABLES    IN SCHEMA ps FROM PUBLIC;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA ps FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA ps FROM PUBLIC;

DO $$
DECLARE r record;
BEGIN
  FOR r IN SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
           WHERE n.nspname = 'ps' AND c.relkind = 'r'
  LOOP
    IF r.relname LIKE 'cat\_%' OR r.relname = 'schema_migrations' THEN
      EXECUTE format('GRANT SELECT ON %I TO privacy_app', r.relname);                      -- sola lettura
    ELSIF r.relname IN ('audit_events','request_events','incident_actions','signature_evidence',
                        'disposal_records','request_extensions','notice_deliveries','files') THEN
      EXECUTE format('GRANT SELECT, INSERT ON %I TO privacy_app', r.relname);              -- eventi e file: niente UPDATE/DELETE
    ELSE
      EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON %I TO privacy_app', r.relname);
    END IF;
  END LOOP;
END $$;

GRANT EXECUTE ON FUNCTION is_non_working(date), compute_due_date(date, int), easter_monday(int),
                          validate_activity_publish(activity_versions),   -- richiamata dal trigger con i privilegi del chiamante
                          verify_audit_chain(), dispose_file(uuid), remove_subject(uuid, uuid, text)
  TO privacy_app;

COMMIT;
