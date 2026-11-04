-- 004_disposal_ledger.sql — registro delle eliminazioni riapplicabile dopo un ripristino (F05) e cancellazione fisica.
-- Eseguire come privacy_migrator dopo 003.
-- Problema risolto: un ripristino da un backup precedente a una cancellazione riporterebbe i dati. Il registro
-- `disposal_items` contiene SOLO tipo e identificativo (nessun dato personale); l'applicazione ne conserva una copia
-- FUORI dal database (file accodato) e, dopo un ripristino, riapplica le eliminazioni con dispose_item().
BEGIN;
SET LOCAL search_path = ps, pg_catalog;

CREATE TABLE disposal_items (
  id          uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  disposal_id uuid        NOT NULL REFERENCES disposal_records(id) ON DELETE RESTRICT,
  item_type   text        NOT NULL CHECK (item_type IN ('rights_request','incident','notice_delivery','data_subject')),
  item_id     uuid        NOT NULL,
  disposed_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (item_type, item_id)
);
CREATE INDEX ix_disposal_items_disposal ON disposal_items (disposal_id);

-- le tabelle di eventi consentono la cancellazione fisica solo al proprietario (cioè alle funzioni SECURITY DEFINER qui sotto)
CREATE OR REPLACE FUNCTION forbid_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' AND current_user = 'privacy_migrator'
     AND TG_TABLE_NAME IN ('request_events','request_extensions','incident_actions','signature_evidence') THEN
    RETURN OLD;
  END IF;
  RAISE EXCEPTION 'la tabella % è append-only: % non ammesso', TG_TABLE_NAME, TG_OP
    USING ERRCODE = 'restrict_violation';
END $$;

CREATE OR REPLACE FUNCTION guard_delivery_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    IF current_user = 'privacy_migrator' THEN RETURN OLD; END IF;   -- solo dalla procedura di eliminazione
    RAISE EXCEPTION 'le consegne non si eliminano' USING ERRCODE = 'restrict_violation';
  END IF;
  IF current_user <> 'privacy_migrator'
     OR (to_jsonb(NEW) - 'subject_name_snapshot' - 'signatory_name_snapshot' - 'signatory_capacity_snapshot' - 'note')
        <> (to_jsonb(OLD) - 'subject_name_snapshot' - 'signatory_name_snapshot' - 'signatory_capacity_snapshot' - 'note') THEN
    RAISE EXCEPTION 'le consegne sono immutabili' USING ERRCODE = 'restrict_violation';
  END IF;
  RETURN NEW;
END $$;

-- rimozione interessato: ora registra anche l'elemento nel registro riapplicabile
CREATE OR REPLACE FUNCTION remove_subject(p_subject uuid, p_actor uuid, p_reason text) RETURNS void
LANGUAGE plpgsql SECURITY DEFINER SET search_path = ps, pg_catalog AS $$
DECLARE s data_subjects; n_del int := 0; n_files int := 0; f uuid; did uuid;
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
    VALUES (p_actor, 'notice_proof', 'Rimozione interessato', n_del + n_files, p_reason) RETURNING id INTO did;
  INSERT INTO disposal_items (disposal_id, item_type, item_id) VALUES (did, 'data_subject', p_subject);
  INSERT INTO audit_events (actor_id, action_code, outcome_code, correlation_id, target_type, target_id, reason_code)
    VALUES (p_actor, 'subject.remove', 'success', gen_random_uuid(), 'data_subject', p_subject, 'removal');
END $$;

-- Eliminazione fisica di un elemento scaduto, autorizzata dal titolare. Restituisce false se l'elemento non c'è
-- (già eliminato o assente dopo un ripristino): così la riapplicazione è idempotente.
-- Richieste e incidenti si eliminano solo se chiusi. Registra disposal_record + disposal_items + audit.
CREATE FUNCTION dispose_item(p_type text, p_id uuid, p_actor uuid, p_reason text, p_policy uuid DEFAULT NULL) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path = ps, pg_catalog AS $$
DECLARE cls text; did uuid; f uuid; s text;
BEGIN
  IF p_reason IS NULL OR btrim(p_reason) = '' THEN RAISE EXCEPTION 'motivazione obbligatoria'; END IF;

  IF p_type = 'data_subject' THEN
    SELECT status_code INTO s FROM data_subjects WHERE id = p_id;
    IF s IS NULL OR s = 'removed' THEN RETURN false; END IF;
    PERFORM remove_subject(p_id, p_actor, p_reason);
    RETURN true;
  END IF;

  IF p_type = 'rights_request' THEN
    cls := 'requests';
    PERFORM 1 FROM rights_requests WHERE id = p_id AND closed_at IS NOT NULL FOR UPDATE;
    IF NOT FOUND THEN
      IF EXISTS (SELECT 1 FROM rights_requests WHERE id = p_id) THEN RAISE EXCEPTION 'richiesta non chiusa: non eliminabile'; END IF;
      RETURN false;
    END IF;
    FOR f IN SELECT file_id FROM request_events WHERE request_id = p_id AND file_id IS NOT NULL
             UNION SELECT notification_file_id FROM request_extensions WHERE request_id = p_id AND notification_file_id IS NOT NULL
             UNION SELECT response_file_id FROM request_decisions WHERE request_id = p_id AND response_file_id IS NOT NULL
    LOOP PERFORM dispose_file(f); END LOOP;
    DELETE FROM request_decisions WHERE request_id = p_id;
    DELETE FROM request_extensions WHERE request_id = p_id;
    DELETE FROM request_events WHERE request_id = p_id;
    DELETE FROM request_rights WHERE request_id = p_id;
    DELETE FROM rights_requests WHERE id = p_id;

  ELSIF p_type = 'incident' THEN
    cls := 'incidents';
    PERFORM 1 FROM incidents WHERE id = p_id AND closed_at IS NOT NULL FOR UPDATE;
    IF NOT FOUND THEN
      IF EXISTS (SELECT 1 FROM incidents WHERE id = p_id) THEN RAISE EXCEPTION 'incidente non chiuso: non eliminabile'; END IF;
      RETURN false;
    END IF;
    FOR f IN SELECT evidence_file_id FROM incident_actions WHERE incident_id = p_id AND evidence_file_id IS NOT NULL
    LOOP PERFORM dispose_file(f); END LOOP;
    DELETE FROM incident_actions WHERE incident_id = p_id;
    DELETE FROM incident_data_categories WHERE assessment_id IN (SELECT id FROM incident_assessments WHERE incident_id = p_id);
    DELETE FROM incident_systems WHERE assessment_id IN (SELECT id FROM incident_assessments WHERE incident_id = p_id);
    DELETE FROM incident_assessments WHERE incident_id = p_id;
    DELETE FROM incidents WHERE id = p_id;

  ELSIF p_type = 'notice_delivery' THEN
    cls := 'notice_proof';
    PERFORM 1 FROM notice_deliveries WHERE id = p_id FOR UPDATE;
    IF NOT FOUND THEN RETURN false; END IF;
    FOR f IN SELECT rendered_file_id FROM notice_deliveries WHERE id = p_id AND rendered_file_id IS NOT NULL
             UNION SELECT signed_file_id FROM signature_evidence WHERE delivery_id = p_id
    LOOP PERFORM dispose_file(f); END LOOP;
    DELETE FROM signature_evidence WHERE delivery_id = p_id;
    DELETE FROM notice_deliveries WHERE id = p_id;
  ELSE
    RAISE EXCEPTION 'tipo di elemento non ammesso: %', p_type;
  END IF;

  INSERT INTO disposal_records (performed_by, record_class_code, generic_description, item_count, policy_version_id, reason)
    VALUES (p_actor, cls, 'Eliminazione elemento scaduto', 1, p_policy, p_reason) RETURNING id INTO did;
  INSERT INTO disposal_items (disposal_id, item_type, item_id) VALUES (did, p_type, p_id);
  INSERT INTO audit_events (actor_id, action_code, outcome_code, correlation_id, target_type, target_id, reason_code)
    VALUES (p_actor, 'disposal.' || p_type, 'success', gen_random_uuid(), p_type, p_id, 'retention');
  RETURN true;
END $$;

REVOKE ALL ON FUNCTION dispose_item(text, uuid, uuid, text, uuid), remove_subject(uuid, uuid, text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION dispose_item(text, uuid, uuid, text, uuid), remove_subject(uuid, uuid, text) TO privacy_app;
GRANT SELECT ON disposal_items TO privacy_app;

COMMIT;
