-- tests/test_v1.sql — verifica delle invarianti del database (documento 03: T01–T22 per la parte DB).
-- Eseguire come superuser DOPO le migrazioni 000–003, su un database di prova:
--   psql -U postgres -d privacy -v ON_ERROR_STOP=1 -f tests/test_v1.sql
-- Tutto avviene in una transazione annullata alla fine (ROLLBACK): nessun dato resta.
-- I passi «as privacy_app» usano il ruolo operativo, per provare anche i privilegi.
\set ON_ERROR_STOP on
BEGIN;

CREATE SCHEMA ps_test;
CREATE TABLE ps_test.results (n serial, label text, ok boolean, detail text);
GRANT USAGE ON SCHEMA ps_test TO privacy_app;
GRANT SELECT ON ps_test.results TO privacy_app;

CREATE FUNCTION ps_test.u(n int) RETURNS uuid LANGUAGE sql IMMUTABLE AS
  $$ SELECT ('00000000-0000-0000-0000-' || lpad(n::text, 12, '0'))::uuid $$;
GRANT EXECUTE ON FUNCTION ps_test.u(int) TO privacy_app;

-- esegue p_sql come p_role, si aspetta successo
CREATE FUNCTION ps_test.expect_ok(p_role text, p_label text, p_sql text) RETURNS void LANGUAGE plpgsql AS $$
DECLARE msg text;
BEGIN
  BEGIN
    EXECUTE format('SET LOCAL ROLE %I', p_role);
    EXECUTE p_sql;
    RESET ROLE;
  EXCEPTION WHEN OTHERS THEN
    GET STACKED DIAGNOSTICS msg = MESSAGE_TEXT;
    RESET ROLE;
    INSERT INTO ps_test.results (label, ok, detail) VALUES (p_label, false, 'errore inatteso: ' || msg);
    RETURN;
  END;
  INSERT INTO ps_test.results (label, ok) VALUES (p_label, true);
END $$;

-- esegue p_sql come p_role, si aspetta un errore il cui testo corrisponde a p_pattern
CREATE FUNCTION ps_test.expect_fail(p_role text, p_label text, p_sql text, p_pattern text) RETURNS void LANGUAGE plpgsql AS $$
DECLARE msg text; failed boolean := false;
BEGIN
  BEGIN
    EXECUTE format('SET LOCAL ROLE %I', p_role);
    EXECUTE p_sql;
    RESET ROLE;
  EXCEPTION WHEN OTHERS THEN
    GET STACKED DIAGNOSTICS msg = MESSAGE_TEXT;
    RESET ROLE;
    failed := true;
  END;
  IF NOT failed THEN
    INSERT INTO ps_test.results (label, ok, detail) VALUES (p_label, false, 'doveva fallire');
  ELSIF msg ~* p_pattern THEN
    INSERT INTO ps_test.results (label, ok) VALUES (p_label, true);
  ELSE
    INSERT INTO ps_test.results (label, ok, detail) VALUES (p_label, false, 'errore diverso dall''atteso: ' || msg);
  END IF;
END $$;

CREATE FUNCTION ps_test.expect_eq(p_label text, p_actual text, p_expected text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
  INSERT INTO ps_test.results (label, ok, detail)
  VALUES (p_label, p_actual IS NOT DISTINCT FROM p_expected,
          CASE WHEN p_actual IS NOT DISTINCT FROM p_expected THEN NULL
               ELSE format('atteso %L, ottenuto %L', p_expected, p_actual) END);
END $$;

-- ===================================================================== fixture di base (come privacy_app)
SELECT ps_test.expect_ok('privacy_app', 'FIX studio', $q$
  INSERT INTO ps.practices (id, name, controller_name, address, privacy_contact, status_code)
  VALUES (ps_test.u(1), 'Studio Test', 'Dott. Test', 'Via Vecchia 1', 'privacy@example.test', 'active') $q$);
SELECT ps_test.expect_ok('privacy_app', 'FIX utente', $q$
  INSERT INTO ps.users (id, practice_id, username, display_name, password_verifier)
  VALUES (ps_test.u(2), ps_test.u(1), 'Titolare', 'Dott. Test', 'argon2id$v=19$m=65536,t=3,p=4$salt$hash') $q$);
SELECT ps_test.expect_ok('privacy_app', 'FIX interessati', $q$
  INSERT INTO ps.data_subjects (id, practice_id, internal_code, first_name, last_name, tax_code) VALUES
   (ps_test.u(11), ps_test.u(1), 'P-001', 'Anna', 'Rossi', 'RSSNNA80A41H501X'),
   (ps_test.u(12), ps_test.u(1), 'P-002', 'Luca', 'Bianchi', NULL),
   (ps_test.u(13), ps_test.u(1), 'P-003', 'Marta', 'Bianchi', NULL),
   (ps_test.u(14), ps_test.u(1), 'C-001', 'Carlo', 'Verdi', NULL),
   (ps_test.u(15), ps_test.u(1), 'P-005', 'Dario', 'Neri', 'NRIDRA70A01H501Z') $q$);

-- unicità del nome utente normalizzato, codici interni, rappresentanza
SELECT ps_test.expect_fail('privacy_app', 'USR nome utente duplicato (maiuscole)', $q$
  INSERT INTO ps.users (practice_id, username, display_name, password_verifier)
  VALUES (ps_test.u(1), ' TITOLARE ', 'x', 'y') $q$, 'duplicate key|unique');
SELECT ps_test.expect_fail('privacy_app', 'SUBJ codice interno duplicato', $q$
  INSERT INTO ps.data_subjects (practice_id, internal_code, first_name, last_name) VALUES (ps_test.u(1), 'P-001', 'A', 'B') $q$, 'duplicate key|unique');
SELECT ps_test.expect_ok('privacy_app', 'SUBJ stesso nome e cognome ammesso (nessuna unicità sul nome)', $q$
  INSERT INTO ps.data_subjects (practice_id, internal_code, first_name, last_name) VALUES (ps_test.u(1), 'P-099', 'Anna', 'Rossi') $q$);
SELECT ps_test.expect_fail('privacy_app', 'REP rappresentante uguale al rappresentato', $q$
  INSERT INTO ps.representations (subject_id, representative_id, capacity_code, scope, valid_from)
  VALUES (ps_test.u(12), ps_test.u(12), 'guardian', 'x', '2025-01-01') $q$, 'check');
SELECT ps_test.expect_fail('privacy_app', 'REP fine prima dell''inizio', $q$
  INSERT INTO ps.representations (subject_id, representative_id, capacity_code, scope, valid_from, valid_until)
  VALUES (ps_test.u(12), ps_test.u(13), 'guardian', 'x', '2025-01-01', '2024-01-01') $q$, 'check');
SELECT ps_test.expect_ok('privacy_app', 'REP rappresentanza verificata (valida fino al 30/06/2026)', $q$
  INSERT INTO ps.representations (id, subject_id, representative_id, capacity_code, scope, valid_from, valid_until, verified_at, verified_by)
  VALUES (ps_test.u(41), ps_test.u(12), ps_test.u(13), 'parental_responsibility', 'informative e moduli', '2025-01-01', '2026-06-30', now(), ps_test.u(2)) $q$);
SELECT ps_test.expect_ok('privacy_app', 'REP rappresentanza non verificata', $q$
  INSERT INTO ps.representations (id, subject_id, representative_id, capacity_code, scope, valid_from)
  VALUES (ps_test.u(42), ps_test.u(12), ps_test.u(14), 'delegate', 'ritiro documenti', '2025-01-01') $q$);

-- ===================================================================== T01/T14 registro: completezza e immutabilità
SELECT ps_test.expect_ok('privacy_app', 'REG attività e versione 1 (bozza incompleta ammessa)', $q$
  INSERT INTO ps.processing_activities (id, practice_id, code, name) VALUES (ps_test.u(21), ps_test.u(1), 'ACT-01', 'Gestione pazienti');
  INSERT INTO ps.activity_versions (id, activity_id, version_number, description) VALUES (ps_test.u(22), ps_test.u(21), 1, 'Anagrafica e informative') $q$);
SELECT ps_test.expect_fail('privacy_app', 'T01 pubblicazione diretta da bozza rifiutata', $q$
  UPDATE ps.activity_versions SET status_code = 'published' WHERE id = ps_test.u(22) $q$, 'in verifica');
SELECT ps_test.expect_ok('privacy_app', 'REG passaggio a in_review', $q$
  UPDATE ps.activity_versions SET status_code = 'in_review' WHERE id = ps_test.u(22) $q$);
SELECT ps_test.expect_fail('privacy_app', 'T01 pubblicazione di scheda priva dei contenuti rifiutata', $q$
  UPDATE ps.activity_versions SET status_code = 'published' WHERE id = ps_test.u(22) $q$, 'pubblicazione rifiutata');
SELECT ps_test.expect_ok('privacy_app', 'REG completamento finalità, basi, categorie', $q$
  INSERT INTO ps.purpose_versions (id, activity_version_id, name, description, legal_assessment, consent_assessment_code)
    VALUES (ps_test.u(23), ps_test.u(22), 'Cura', 'Prestazioni sanitarie', 'Art. 9(2)(h) e 9(3): cura da professionista tenuto al segreto', 'not_required');
  INSERT INTO ps.purpose_legal_grounds (purpose_version_id, article6_code, article9_code, scope, assessment_note)
    VALUES (ps_test.u(23), 'public_task', 'health_care', 'prestazioni di cura', 'valutazione del titolare del test');
  INSERT INTO ps.activity_data_categories VALUES (ps_test.u(22), 'identification'), (ps_test.u(22), 'health_relation');
  INSERT INTO ps.activity_subject_categories VALUES (ps_test.u(22), 'patients');
  UPDATE ps.activity_versions SET review_due_on = '2027-11-04', approved_by = ps_test.u(2), approved_at = now() WHERE id = ps_test.u(22) $q$);
SELECT ps_test.expect_fail('privacy_app', 'T01 art. 9 senza condizione dell''art. 9 rifiutato (versione 2)', $q$
  DO $d$ BEGIN
    INSERT INTO ps.activity_versions (id, activity_id, version_number, description, review_due_on, approved_by, approved_at)
      VALUES (ps_test.u(26), ps_test.u(21), 2, 'v2', '2027-11-04', ps_test.u(2), now());
    INSERT INTO ps.purpose_versions (id, activity_version_id, name, description, legal_assessment, consent_assessment_code)
      VALUES (ps_test.u(27), ps_test.u(26), 'Cura', 'd', 'v', 'not_required');
    INSERT INTO ps.purpose_legal_grounds (purpose_version_id, article6_code, scope, assessment_note) VALUES (ps_test.u(27), 'public_task', 's', 'n');
    INSERT INTO ps.activity_data_categories VALUES (ps_test.u(26), 'health_relation');
    INSERT INTO ps.activity_subject_categories VALUES (ps_test.u(26), 'patients');
    UPDATE ps.activity_versions SET status_code = 'in_review' WHERE id = ps_test.u(26);
    UPDATE ps.activity_versions SET status_code = 'published' WHERE id = ps_test.u(26);
  END $d$ $q$, 'art\. 9');
SELECT ps_test.expect_ok('privacy_app', 'T01 pubblicazione della versione completa', $q$
  UPDATE ps.activity_versions SET status_code = 'published' WHERE id = ps_test.u(22) $q$);
SELECT ps_test.expect_fail('privacy_app', 'T14 contenuto della versione pubblicata immutabile', $q$
  UPDATE ps.activity_versions SET description = 'alterata' WHERE id = ps_test.u(22) $q$, 'immutabile');
SELECT ps_test.expect_fail('privacy_app', 'T14 finalità non aggiungibile a versione pubblicata', $q$
  INSERT INTO ps.purpose_versions (activity_version_id, name) VALUES (ps_test.u(22), 'Extra') $q$, 'non modificabile');
SELECT ps_test.expect_fail('privacy_app', 'T14 base giuridica non modificabile in versione pubblicata', $q$
  UPDATE ps.purpose_legal_grounds SET scope = 'altro' WHERE purpose_version_id = ps_test.u(23) $q$, 'non modificabile');
SELECT ps_test.expect_fail('privacy_app', 'T14 versione pubblicata non eliminabile', $q$
  DELETE FROM ps.activity_versions WHERE id = ps_test.u(22) $q$, 'bozze');
SELECT ps_test.expect_fail('privacy_app', 'T01 una versione non nasce già pubblicata (si aggira la validazione)', $q$
  INSERT INTO ps.activity_versions (activity_id, version_number, description, status_code, review_due_on, approved_by, approved_at)
  VALUES (ps_test.u(21), 3, 'diretta', 'published', '2027-01-01', ps_test.u(2), now()) $q$, 'bozza');
SELECT ps_test.expect_fail('privacy_app', 'T01 un documento non nasce già pubblicato', $q$
  INSERT INTO ps.document_versions (document_id, version_number, language_code, body, status_code)
  VALUES (ps_test.u(31), 9, 'it', 'diretto', 'published') $q$, 'bozza');

-- ===================================================================== T01/T14 documenti
SELECT ps_test.expect_ok('privacy_app', 'DOC informativa e versione 1 in bozza con segnaposto', $q$
  INSERT INTO ps.documents (id, practice_id, type_code, title) VALUES (ps_test.u(31), ps_test.u(1), 'notice', 'Informativa pazienti');
  INSERT INTO ps.document_versions (id, document_id, version_number, language_code, body)
    VALUES (ps_test.u(32), ps_test.u(31), 1, 'it', 'Titolare: Dott. Test - {{address}}') $q$);
SELECT ps_test.expect_fail('privacy_app', 'T01 approvazione con segnaposto irrisolti rifiutata', $q$
  UPDATE ps.document_versions SET status_code = 'approved', approved_by = ps_test.u(2), approved_at = now(),
         content_hmac = sha256('x'::bytea) WHERE id = ps_test.u(32) $q$, 'segnaposto');
SELECT ps_test.expect_ok('privacy_app', 'T01 approvazione del testo risolto', $q$
  UPDATE ps.document_versions SET body = 'Titolare: Dott. Test - Via Vecchia 1', status_code = 'approved',
         approved_by = ps_test.u(2), approved_at = now(), content_hmac = sha256('x'::bytea) WHERE id = ps_test.u(32) $q$);
SELECT ps_test.expect_fail('privacy_app', 'T01 pubblicazione informativa senza collegamento al registro rifiutata', $q$
  UPDATE ps.document_versions SET status_code = 'published' WHERE id = ps_test.u(32) $q$, 'collegamento al registro');
SELECT ps_test.expect_ok('privacy_app', 'DOC collegamento al registro (versione pubblicata)', $q$
  INSERT INTO ps.notice_activity_versions VALUES (ps_test.u(32), ps_test.u(22)) $q$);
SELECT ps_test.expect_ok('privacy_app', 'DOC pubblicazione informativa', $q$
  UPDATE ps.document_versions SET status_code = 'published', effective_from = '2026-11-04' WHERE id = ps_test.u(32) $q$);
SELECT ps_test.expect_fail('privacy_app', 'T14 testo pubblicato immutabile', $q$
  UPDATE ps.document_versions SET body = 'nuovo indirizzo' WHERE id = ps_test.u(32) $q$, 'immutabile');
SELECT ps_test.expect_fail('privacy_app', 'T14 collegamenti di versione pubblicata immutabili', $q$
  DELETE FROM ps.notice_activity_versions WHERE document_version_id = ps_test.u(32) $q$, 'non modificabile');

-- ===================================================================== T03/T04/T11 consegne
SELECT ps_test.expect_ok('privacy_app', 'T03 consegna senza firma registrata', $q$
  INSERT INTO ps.notice_deliveries (id, document_version_id, subject_id, delivered_at, channel_code, receipt_status_code, subject_name_snapshot, recorded_by)
  VALUES (ps_test.u(51), ps_test.u(32), ps_test.u(11), '2026-11-04 10:00+01', 'in_person', 'delivered_unsigned', 'Anna Rossi', ps_test.u(2)) $q$);
SELECT ps_test.expect_fail('privacy_app', 'T03 tentativo fallito con data di consegna rifiutato', $q$
  INSERT INTO ps.notice_deliveries (document_version_id, subject_id, delivered_at, channel_code, receipt_status_code, subject_name_snapshot, recorded_by)
  VALUES (ps_test.u(32), ps_test.u(11), now(), 'post', 'failed_attempt', 'Anna Rossi', ps_test.u(2)) $q$, 'check');
SELECT ps_test.expect_ok('privacy_app', 'T03 tentativo fallito senza data', $q$
  INSERT INTO ps.notice_deliveries (document_version_id, subject_id, channel_code, receipt_status_code, subject_name_snapshot, recorded_by)
  VALUES (ps_test.u(32), ps_test.u(11), 'post', 'failed_attempt', 'Anna Rossi', ps_test.u(2)) $q$);
SELECT ps_test.expect_ok('privacy_app', 'T04 rappresentante con poteri validi alla data', $q$
  INSERT INTO ps.notice_deliveries (document_version_id, subject_id, representation_id, delivered_at, channel_code, receipt_status_code, subject_name_snapshot, signatory_name_snapshot, signatory_capacity_snapshot, recorded_by)
  VALUES (ps_test.u(32), ps_test.u(12), ps_test.u(41), '2026-03-01 09:00+01', 'in_person', 'signed_receipt', 'Luca Bianchi', 'Marta Bianchi', 'responsabilità genitoriale', ps_test.u(2)) $q$);
SELECT ps_test.expect_fail('privacy_app', 'T04 poteri scaduti non usabili per una nuova consegna', $q$
  INSERT INTO ps.notice_deliveries (document_version_id, subject_id, representation_id, delivered_at, channel_code, receipt_status_code, subject_name_snapshot, recorded_by)
  VALUES (ps_test.u(32), ps_test.u(12), ps_test.u(41), '2026-07-15 09:00+02', 'in_person', 'delivered_unsigned', 'Luca Bianchi', ps_test.u(2)) $q$, 'non validi');
SELECT ps_test.expect_fail('privacy_app', 'T04 poteri non verificati non usabili', $q$
  INSERT INTO ps.notice_deliveries (document_version_id, subject_id, representation_id, delivered_at, channel_code, receipt_status_code, subject_name_snapshot, recorded_by)
  VALUES (ps_test.u(32), ps_test.u(12), ps_test.u(42), '2026-03-01 09:00+01', 'in_person', 'delivered_unsigned', 'Luca Bianchi', ps_test.u(2)) $q$, 'non verificati');
SELECT ps_test.expect_fail('privacy_app', 'T13 rappresentanza riferita a un altro interessato respinta', $q$
  INSERT INTO ps.notice_deliveries (document_version_id, subject_id, representation_id, delivered_at, channel_code, receipt_status_code, subject_name_snapshot, recorded_by)
  VALUES (ps_test.u(32), ps_test.u(11), ps_test.u(41), '2026-03-01 09:00+01', 'in_person', 'delivered_unsigned', 'Anna Rossi', ps_test.u(2)) $q$, 'foreign key|violates');
SELECT ps_test.expect_fail('privacy_app', 'T03 consegna di versione non pubblicata rifiutata', $q$
  DO $d$ BEGIN
    INSERT INTO ps.document_versions (id, document_id, version_number, language_code, body) VALUES (ps_test.u(33), ps_test.u(31), 2, 'it', 'bozza');
    INSERT INTO ps.notice_deliveries (document_version_id, subject_id, delivered_at, channel_code, receipt_status_code, subject_name_snapshot, recorded_by)
      VALUES (ps_test.u(33), ps_test.u(11), now(), 'in_person', 'delivered_unsigned', 'Anna Rossi', ps_test.u(2));
  END $d$ $q$, 'non è pubblicata');
SELECT ps_test.expect_fail('privacy_app', 'T22 codice di consegna duplicato', $q$
  INSERT INTO ps.notice_deliveries (delivery_code, document_version_id, subject_id, delivered_at, channel_code, receipt_status_code, subject_name_snapshot, recorded_by)
  SELECT delivery_code, document_version_id, subject_id, delivered_at, channel_code, receipt_status_code, subject_name_snapshot, recorded_by
    FROM ps.notice_deliveries WHERE id = ps_test.u(51) $q$, 'duplicate key|unique');
SELECT ps_test.expect_fail('privacy_app', 'T11 consegne non modificabili (privilegi)', $q$
  UPDATE ps.notice_deliveries SET subject_name_snapshot = 'x' WHERE id = ps_test.u(51) $q$, 'permission denied');
SELECT ps_test.expect_fail('postgres', 'T11 consegne non modificabili (trigger, anche per un ruolo privilegiato)', $q$
  UPDATE ps.notice_deliveries SET subject_name_snapshot = 'x' WHERE id = ps_test.u(51) $q$, 'immutabili');

-- T11/T14: cambiare l'indirizzo dello studio non altera il testo già pubblicato né la consegna
SELECT ps_test.expect_ok('privacy_app', 'T11 modifica dell''indirizzo dello studio', $q$
  UPDATE ps.practices SET address = 'Via Nuova 99' WHERE id = ps_test.u(1) $q$);
SELECT ps_test.expect_eq('T11 testo pubblicato invariato dopo la modifica',
  (SELECT body FROM ps.document_versions WHERE id = ps_test.u(32)), 'Titolare: Dott. Test - Via Vecchia 1');
SELECT ps_test.expect_eq('T15 row_version incrementata dall''aggiornamento',
  (SELECT row_version::text FROM ps.practices WHERE id = ps_test.u(1)), '2');

-- ===================================================================== T05/T06/T07 richieste e termini
-- Pasqua/lunedì dell'Angelo
SELECT ps_test.expect_eq('T06 lunedì dell''Angelo 2025', ps.easter_monday(2025)::text, '2025-04-21');
SELECT ps_test.expect_eq('T06 lunedì dell''Angelo 2026', ps.easter_monday(2026)::text, '2026-04-06');
SELECT ps_test.expect_eq('T06 lunedì dell''Angelo 2027', ps.easter_monday(2027)::text, '2027-03-29');
SELECT ps_test.expect_eq('T06 lunedì dell''Angelo 2028', ps.easter_monday(2028)::text, '2028-04-17');

-- (ricevuta, mesi, attesa) calcolati A MANO dal calendario, non dalla funzione
SELECT ps_test.expect_eq('T06 ' || c.r || ' +' || c.m || 'm', ps.compute_due_date(c.r::date, c.m)::text, c.expected)
FROM (VALUES
  ('2026-03-15', 1, '2026-04-15'),   -- regola ordinaria (mercoledì)
  ('2026-01-31', 1, '2026-03-02'),   -- 28/02/2026 è sabato -> lunedì 2 marzo
  ('2028-01-31', 1, '2028-02-29'),   -- bisestile, martedì
  ('2026-11-30', 1, '2026-12-30'),   -- mercoledì
  ('2026-12-31', 1, '2027-02-01'),   -- 31/01/2027 domenica -> lunedì
  ('2026-03-25', 1, '2026-04-27'),   -- 25 aprile sabato e festa -> lunedì 27
  ('2026-03-06', 1, '2026-04-07'),   -- 6 aprile = lunedì dell'Angelo -> martedì
  ('2026-04-01', 1, '2026-05-04'),   -- 1° maggio venerdì festa -> lunedì 4
  ('2026-05-02', 1, '2026-06-03'),   -- 2 giugno festa -> mercoledì
  ('2026-11-25', 1, '2026-12-28'),   -- 25 dic. venerdì, 26 festa, weekend -> lunedì 28
  ('2027-02-28', 1, '2027-03-30'),   -- 28/03 domenica, 29/03 lunedì dell'Angelo -> martedì
  ('2026-01-31', 3, '2026-04-30')    -- limite complessivo di proroga (3 mesi)
) AS c(r, m, expected);

SELECT ps_test.expect_ok('privacy_app', 'T06 festività locale configurabile', $q$
  INSERT INTO ps.local_holidays VALUES ('2026-09-29', 'San Michele (patrono di prova)') $q$);
SELECT ps_test.expect_eq('T06 festività locale sposta la scadenza (29/09/2026 martedì)',
  ps.compute_due_date('2026-08-29', 1)::text, '2026-09-30');

SELECT ps_test.expect_ok('privacy_app', 'T05 richiesta di un erede, senza paziente fittizio', $q$
  INSERT INTO ps.rights_requests (id, practice_id, subject_id, requester_name, requester_capacity_code, reference_code, received_at, channel_code, request_text, assignee_id)
  VALUES (ps_test.u(61), ps_test.u(1), NULL, 'Giulia Verdi', 'heir_or_interested', 'R-2026-001', '2026-01-31 10:00+01', 'email', 'Richiesta di accesso ai dati di un familiare defunto', ps_test.u(2)) $q$);
SELECT ps_test.expect_fail('privacy_app', 'T05 richiesta senza soggetto né richiedente rifiutata', $q$
  INSERT INTO ps.rights_requests (practice_id, reference_code, received_at, channel_code, request_text, assignee_id)
  VALUES (ps_test.u(1), 'R-2026-002', now(), 'email', 'x', ps_test.u(2)) $q$, 'check');
SELECT ps_test.expect_eq('T06 termine iniziale calcolato alla creazione (31/01 -> 02/03)',
  (SELECT initial_due_on::text FROM ps.rights_requests WHERE id = ps_test.u(61)), '2026-03-02');
SELECT ps_test.expect_ok('privacy_app', 'T06 ricezione a cavallo di mezzanotte (UTC vs Europe/Rome)', $q$
  INSERT INTO ps.rights_requests (id, practice_id, subject_id, reference_code, received_at, channel_code, request_text, assignee_id)
  VALUES (ps_test.u(62), ps_test.u(1), ps_test.u(11), 'R-2026-003', '2026-03-31 23:30+00', 'email', 'Rettifica', ps_test.u(2)) $q$);
SELECT ps_test.expect_eq('T06 31/03 23:30 UTC = 01/04 Roma -> 04/05',
  (SELECT initial_due_on::text FROM ps.rights_requests WHERE id = ps_test.u(62)), '2026-05-04');

SELECT ps_test.expect_fail('privacy_app', 'T07 il termine non si modifica direttamente', $q$
  UPDATE ps.rights_requests SET effective_due_on = '2026-04-30' WHERE id = ps_test.u(61) $q$, 'proroga');
SELECT ps_test.expect_fail('privacy_app', 'T07 proroga oltre il limite complessivo rifiutata', $q$
  INSERT INTO ps.request_extensions (request_id, new_due_on, reason, decided_at)
  VALUES (ps_test.u(61), '2026-05-15', 'troppo', now()) $q$, 'oltre il limite');
SELECT ps_test.expect_ok('privacy_app', 'T07 proroga motivata registrata', $q$
  INSERT INTO ps.request_extensions (request_id, new_due_on, reason, decided_at, notified_at)
  VALUES (ps_test.u(61), '2026-04-30', 'complessità della richiesta', '2026-02-20 10:00+01', '2026-02-20 10:05+01') $q$);
SELECT ps_test.expect_eq('T07 termine effettivo aggiornato',
  (SELECT effective_due_on::text FROM ps.rights_requests WHERE id = ps_test.u(61)), '2026-04-30');
SELECT ps_test.expect_eq('T07 termine iniziale conservato',
  (SELECT initial_due_on::text FROM ps.rights_requests WHERE id = ps_test.u(61)), '2026-03-02');
SELECT ps_test.expect_fail('privacy_app', 'T07 termine iniziale non modificabile', $q$
  UPDATE ps.rights_requests SET initial_due_on = '2026-05-01', effective_due_on = '2026-05-01' WHERE id = ps_test.u(61) $q$, 'non modificabil');
SELECT ps_test.expect_fail('privacy_app', 'T07 chiusura senza diritti classificati rifiutata', $q$
  UPDATE ps.rights_requests SET status_code = 'closed', closed_at = now() WHERE id = ps_test.u(61) $q$, 'nessun diritto');
SELECT ps_test.expect_ok('privacy_app', 'T07 diritto classificato', $q$
  INSERT INTO ps.request_rights VALUES (ps_test.u(61), 'access') $q$);
SELECT ps_test.expect_fail('privacy_app', 'T07 chiusura senza decisione e riscontro rifiutata', $q$
  UPDATE ps.rights_requests SET status_code = 'closed', closed_at = now() WHERE id = ps_test.u(61) $q$, 'senza decisione');
SELECT ps_test.expect_fail('privacy_app', 'T07 decisione negativa senza motivazione rifiutata', $q$
  INSERT INTO ps.request_decisions (request_id, right_code, outcome_code, decided_by, decided_at)
  VALUES (ps_test.u(61), 'access', 'denied', ps_test.u(2), now()) $q$, 'check');
SELECT ps_test.expect_fail('privacy_app', 'T07 decisione su un diritto estraneo alla richiesta', $q$
  INSERT INTO ps.request_decisions (request_id, right_code, outcome_code, decided_by, decided_at)
  VALUES (ps_test.u(61), 'erasure', 'granted', ps_test.u(2), now()) $q$, 'foreign key|violates');
SELECT ps_test.expect_ok('privacy_app', 'T07 decisione con riscontro inviato', $q$
  INSERT INTO ps.request_decisions (request_id, right_code, outcome_code, decided_by, decided_at, sent_at)
  VALUES (ps_test.u(61), 'access', 'granted', ps_test.u(2), '2026-03-01 10:00+01', '2026-03-01 11:00+01') $q$);
SELECT ps_test.expect_ok('privacy_app', 'T07 chiusura con esito e riscontro', $q$
  UPDATE ps.rights_requests SET status_code = 'closed', closed_at = now() WHERE id = ps_test.u(61) $q$);
SELECT ps_test.expect_fail('privacy_app', 'T07 richiesta chiusa non riapribile', $q$
  UPDATE ps.rights_requests SET status_code = 'inquiry', closed_at = NULL WHERE id = ps_test.u(61) $q$, 'già closed|check');
SELECT ps_test.expect_fail('privacy_app', 'T07 annullamento senza evento motivato rifiutato', $q$
  UPDATE ps.rights_requests SET status_code = 'cancelled', closed_at = now() WHERE id = ps_test.u(62) $q$, 'annullamento');
SELECT ps_test.expect_ok('privacy_app', 'T07 annullamento con evento motivato', $q$
  INSERT INTO ps.request_events (request_id, event_type_code, actor_id, description) VALUES (ps_test.u(62), 'cancelled', ps_test.u(2), 'registrata per errore');
  UPDATE ps.rights_requests SET status_code = 'cancelled', closed_at = now() WHERE id = ps_test.u(62) $q$);
SELECT ps_test.expect_fail('privacy_app', 'T07 eventi di richiesta append-only', $q$
  UPDATE ps.request_events SET description = 'x' WHERE request_id = ps_test.u(62) $q$, 'append-only|permission');

-- ===================================================================== T08/T09 incidenti
SELECT ps_test.expect_ok('privacy_app', 'T08 incidente di sola indisponibilità registrato come violazione', $q$
  INSERT INTO ps.incidents (id, practice_id, reference_code, discovered_at, awareness_at, description, classification_code, assignee_id)
  VALUES (ps_test.u(71), ps_test.u(1), 'INC-001', '2026-03-28 11:00+01', '2026-03-28 12:00+01', 'Ransomware: archivio non disponibile', 'breach', ps_test.u(2)) $q$);
SELECT ps_test.expect_eq('T08 72 ore = 259200 secondi anche attraverso il cambio d''ora',
  (SELECT extract(epoch FROM notification_deadline_at - awareness_at)::bigint::text FROM ps.incidents WHERE id = ps_test.u(71)), '259200');
SELECT ps_test.expect_eq('T08 scadenza in ora legale locale (31/03 13:00 Roma)',
  (SELECT to_char(notification_deadline_at AT TIME ZONE 'Europe/Rome', 'YYYY-MM-DD HH24:MI') FROM ps.incidents WHERE id = ps_test.u(71)), '2026-03-31 13:00');
SELECT ps_test.expect_fail('privacy_app', 'T09 notifica senza protocollo rifiutata', $q$
  INSERT INTO ps.incident_actions (incident_id, type_code, occurred_at, description, actor_id)
  VALUES (ps_test.u(71), 'notification', now(), 'Notifica inviata', ps_test.u(2)) $q$, 'check');
SELECT ps_test.expect_ok('privacy_app', 'T09 notifica con protocollo', $q$
  INSERT INTO ps.incident_actions (incident_id, type_code, occurred_at, description, actor_id, protocol_reference)
  VALUES (ps_test.u(71), 'notification', now(), 'Notifica inviata dal portale', ps_test.u(2), 'PROT-TEST-0001') $q$);
SELECT ps_test.expect_fail('privacy_app', 'T09 azioni dell''incidente append-only', $q$
  UPDATE ps.incident_actions SET description = 'x' WHERE incident_id = ps_test.u(71) $q$, 'append-only|permission');

-- ===================================================================== T12 conservazione
SELECT ps_test.expect_fail('privacy_app', 'T12 durata «fissa» senza durata rifiutata', $q$
  INSERT INTO ps.retention_policy_versions (practice_id, record_class_code, trigger_code, duration_kind_code, review_due_on)
  VALUES (ps_test.u(1), 'fiscal', 'document_date', 'fixed', '2027-01-01') $q$, 'check');
SELECT ps_test.expect_fail('privacy_app', 'T12 «illimitata» senza criterio rifiutata (NULL non vale illimitata)', $q$
  INSERT INTO ps.retention_policy_versions (practice_id, record_class_code, trigger_code, duration_kind_code, review_due_on)
  VALUES (ps_test.u(1), 'clinical', 'last_visit', 'indefinite', '2027-01-01') $q$, 'check');
SELECT ps_test.expect_fail('privacy_app', 'T12 «illimitata» con una durata rifiutata', $q$
  INSERT INTO ps.retention_policy_versions (practice_id, record_class_code, trigger_code, duration_kind_code, duration_months, criterion, review_due_on)
  VALUES (ps_test.u(1), 'clinical', 'last_visit', 'indefinite', 12, 'x', '2027-01-01') $q$, 'check');
SELECT ps_test.expect_ok('privacy_app', 'T12 «illimitata» motivata (bozza)', $q$
  INSERT INTO ps.retention_policy_versions (practice_id, record_class_code, trigger_code, duration_kind_code, criterion, legal_reference, review_due_on)
  VALUES (ps_test.u(1), 'clinical', 'last_visit', 'indefinite', 'Motivazione di test del titolare', 'riferimento di test', '2027-01-01') $q$);
SELECT ps_test.expect_fail('privacy_app', 'T12 politica approvata richiede un approvatore', $q$
  INSERT INTO ps.retention_policy_versions (practice_id, record_class_code, trigger_code, duration_kind_code, duration_months, review_due_on, status_code)
  VALUES (ps_test.u(1), 'fiscal', 'document_date', 'fixed', 120, '2027-01-01', 'approved') $q$, 'check');

-- ===================================================================== T20 file
SELECT ps_test.expect_fail('privacy_app', 'T20 tipo di file non ammesso', $q$
  INSERT INTO ps.files (media_type, size_bytes, ciphertext, nonce, auth_tag, wrapped_key, key_version, plaintext_hmac)
  VALUES ('application/x-msdownload', 10, '\x00', decode(repeat('00',12),'hex'), decode(repeat('00',16),'hex'), '\x00', 'k1', decode(repeat('00',32),'hex')) $q$, 'check');
SELECT ps_test.expect_fail('privacy_app', 'T20 dimensione eccessiva', $q$
  INSERT INTO ps.files (media_type, size_bytes, ciphertext, nonce, auth_tag, wrapped_key, key_version, plaintext_hmac)
  VALUES ('application/pdf', 99999999, '\x00', decode(repeat('00',12),'hex'), decode(repeat('00',16),'hex'), '\x00', 'k1', decode(repeat('00',32),'hex')) $q$, 'check');
SELECT ps_test.expect_fail('privacy_app', 'T20 nonce di lunghezza errata', $q$
  INSERT INTO ps.files (media_type, size_bytes, ciphertext, nonce, auth_tag, wrapped_key, key_version, plaintext_hmac)
  VALUES ('application/pdf', 10, '\x00', decode(repeat('00',8),'hex'), decode(repeat('00',16),'hex'), '\x00', 'k1', decode(repeat('00',32),'hex')) $q$, 'check');
SELECT ps_test.expect_fail('privacy_app', 'T20 tag di autenticazione assente su file attivo', $q$
  INSERT INTO ps.files (media_type, size_bytes, ciphertext, nonce, wrapped_key, key_version, plaintext_hmac)
  VALUES ('application/pdf', 10, '\x00', decode(repeat('00',12),'hex'), '\x00', 'k1', decode(repeat('00',32),'hex')) $q$, 'check');
SELECT ps_test.expect_eq('T20 nessuna colonna con il nome originale del file',
  (SELECT count(*)::text FROM information_schema.columns WHERE table_schema = 'ps' AND column_name ~* 'original_name|file_name|filename'), '0');
SELECT ps_test.expect_ok('privacy_app', 'T20 file valido (PDF e scansione)', $q$
  INSERT INTO ps.files (id, media_type, size_bytes, ciphertext, nonce, auth_tag, wrapped_key, key_version, plaintext_hmac)
  SELECT ps_test.u(80 + g), CASE g WHEN 1 THEN 'application/pdf' ELSE 'image/tiff' END, 10, '\x0102', decode(repeat('00',12),'hex'),
         decode(repeat('00',16),'hex'), '\x00', 'k1', decode(repeat('0' || g::text, 32),'hex') FROM generate_series(1,2) g $q$);

-- ===================================================================== T10 rimozione di un interessato
SELECT ps_test.expect_ok('privacy_app', 'T10 consegna con PDF e scansione dell''interessato da rimuovere', $q$
  INSERT INTO ps.notice_deliveries (id, document_version_id, subject_id, delivered_at, channel_code, receipt_status_code, subject_name_snapshot, signatory_name_snapshot, rendered_hmac, rendered_file_id, recorded_by, note)
  VALUES (ps_test.u(52), ps_test.u(32), ps_test.u(15), '2026-11-04 11:00+01', 'in_person', 'signed_receipt', 'Dario Neri', 'Dario Neri', decode(repeat('01',32),'hex'), ps_test.u(81), ps_test.u(2), 'nota con dati');
  INSERT INTO ps.signature_evidence (delivery_id, signed_file_id, signature_type_code) VALUES (ps_test.u(52), ps_test.u(82), 'wet_scan') $q$);
SELECT ps_test.expect_fail('privacy_app', 'T10 rimozione senza motivazione rifiutata', $q$
  SELECT ps.remove_subject(ps_test.u(15), ps_test.u(2), ' ') $q$, 'motivazione');
SELECT ps_test.expect_ok('privacy_app', 'T10 rimozione dell''interessato (procedura)', $q$
  SELECT ps.remove_subject(ps_test.u(15), ps_test.u(2), 'Valutazione del titolare del test: cancellazione accolta') $q$);
SELECT ps_test.expect_eq('T10 anagrafica azzerata',
  (SELECT concat_ws('|', status_code, first_name, last_name, coalesce(tax_code,'-'), coalesce(email,'-'), coalesce(birth_date::text,'-')) FROM ps.data_subjects WHERE id = ps_test.u(15)),
  'removed|Interessato|rimosso|-|-|-');
SELECT ps_test.expect_eq('T10 codice interno e identità tecnica conservati',
  (SELECT internal_code FROM ps.data_subjects WHERE id = ps_test.u(15)), 'P-005');
SELECT ps_test.expect_eq('T10 snapshot della consegna anonimizzati, consegna conservata',
  (SELECT concat_ws('|', subject_name_snapshot, coalesce(signatory_name_snapshot,'-'), coalesce(note,'-'), (delivery_code IS NOT NULL)::text) FROM ps.notice_deliveries WHERE id = ps_test.u(52)),
  'Interessato rimosso|-|-|true');
SELECT ps_test.expect_eq('T10 file collegati svuotati',
  (SELECT string_agg(status_code || ':' || (ciphertext IS NULL)::text, ',' ORDER BY id) FROM ps.files WHERE id IN (ps_test.u(81), ps_test.u(82))), 'disposed:true,disposed:true');
SELECT ps_test.expect_eq('T10 nessun file attivo residuo',
  (SELECT count(*)::text FROM ps.files WHERE status_code = 'active' AND ciphertext IS NOT NULL), '0');
SELECT ps_test.expect_eq('T10 resoconto di eliminazione senza dati personali',
  (SELECT generic_description || '|' || item_count FROM ps.disposal_records ORDER BY created_at DESC LIMIT 1), 'Rimozione interessato|3');
SELECT ps_test.expect_eq('T10 evento di audit registrato',
  (SELECT count(*)::text FROM ps.audit_events WHERE action_code = 'subject.remove' AND target_id = ps_test.u(15)), '1');
SELECT ps_test.expect_fail('privacy_app', 'T10 interessato rimosso non modificabile dall''app', $q$
  UPDATE ps.data_subjects SET first_name = 'Dario' WHERE id = ps_test.u(15) $q$, 'rimosso');
SELECT ps_test.expect_fail('privacy_app', 'T10 rimozione ripetuta rifiutata', $q$
  SELECT ps.remove_subject(ps_test.u(15), ps_test.u(2), 'ancora') $q$, 'già rimosso');

-- ===================================================================== T16/T17 privilegi e audit
SELECT ps_test.expect_fail('privacy_app', 'T16 il ruolo operativo non esegue DDL', $q$ CREATE TABLE ps.abusivo (x int) $q$, 'permission denied');
SELECT ps_test.expect_fail('privacy_app', 'T16 il ruolo operativo non elimina tabelle', $q$ DROP TABLE ps.audit_events $q$, 'must be owner|permission denied');
SELECT ps_test.expect_fail('privacy_app', 'T16 il ruolo operativo non disattiva trigger', $q$ ALTER TABLE ps.audit_events DISABLE TRIGGER USER $q$, 'must be owner|permission denied');
SELECT ps_test.expect_fail('privacy_app', 'T16 audit non modificabile dal ruolo operativo', $q$ UPDATE ps.audit_events SET outcome_code = 'success' $q$, 'permission denied');
SELECT ps_test.expect_fail('privacy_app', 'T16 audit non cancellabile dal ruolo operativo', $q$ DELETE FROM ps.audit_events $q$, 'permission denied');
SELECT ps_test.expect_fail('privacy_app', 'T16 audit non svuotabile dal ruolo operativo', $q$ TRUNCATE ps.audit_events $q$, 'permission denied');
SELECT ps_test.expect_fail('privacy_app', 'T16 cataloghi non modificabili dal ruolo operativo', $q$ INSERT INTO ps.cat_right VALUES ('nuovo','x',true) $q$, 'permission denied');
SELECT ps_test.expect_fail('privacy_app', 'T16 file non modificabili dal ruolo operativo', $q$ UPDATE ps.files SET key_version = 'k2' $q$, 'permission denied');
SELECT ps_test.expect_fail('postgres', 'T16 audit append-only anche per un ruolo privilegiato', $q$ UPDATE ps.audit_events SET outcome_code = 'failure' $q$, 'append-only');
SELECT ps_test.expect_fail('privacy_app', 'T22 comando ripetuto con lo stesso identificatore', $q$
  INSERT INTO ps.processed_commands (command_id, command_type) VALUES (ps_test.u(900), 'issue_notice'), (ps_test.u(900), 'issue_notice') $q$, 'duplicate key|unique');

SELECT ps_test.expect_ok('privacy_app', 'T17 eventi di audit aggiunti dall''app', $q$
  INSERT INTO ps.audit_events (actor_id, action_code, outcome_code, correlation_id, target_type, target_id)
  VALUES (ps_test.u(2), 'document.publish', 'success', ps_test.u(901), 'document_version', ps_test.u(32)),
         (ps_test.u(2), 'login', 'denied', ps_test.u(902), NULL, NULL),
         (ps_test.u(2), 'export.registry', 'success', ps_test.u(903), 'practice', ps_test.u(1)) $q$);
SELECT ps_test.expect_eq('T17 catena di audit integra', coalesce((SELECT ps.verify_audit_chain()::text), 'integra'), 'integra');
ALTER TABLE ps.audit_events DISABLE TRIGGER trg_append_only;
DO $$
DECLARE rid bigint; orig text;
BEGIN
  SELECT id, action_code INTO rid, orig FROM ps.audit_events ORDER BY id OFFSET 1 LIMIT 1;
  UPDATE ps.audit_events SET action_code = 'alterato' WHERE id = rid;
  PERFORM ps_test.expect_eq('T17 modifica di una riga rilevata', (ps.verify_audit_chain() = rid)::text, 'true');
  UPDATE ps.audit_events SET action_code = orig WHERE id = rid;
  PERFORM ps_test.expect_eq('T17 catena di nuovo integra dopo il ripristino', coalesce(ps.verify_audit_chain()::text, 'integra'), 'integra');
  DELETE FROM ps.audit_events WHERE id = rid;
  PERFORM ps_test.expect_eq('T17 cancellazione intermedia rilevata', (ps.verify_audit_chain() IS NOT NULL)::text, 'true');
END $$;
ALTER TABLE ps.audit_events ENABLE TRIGGER trg_append_only;

-- ===================================================================== integrità generale del modello
SELECT ps_test.expect_eq('SCHEMA ogni tabella ha una chiave primaria',
  (SELECT count(*)::text FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'ps' AND c.relkind = 'r' AND c.relname NOT LIKE 'cat\_%'
      AND NOT EXISTS (SELECT 1 FROM pg_constraint k WHERE k.conrelid = c.oid AND k.contype = 'p')), '0');
SELECT ps_test.expect_eq('SCHEMA nessun CASCADE verso dati di dominio fuori dai figli tecnici delle bozze',
  (SELECT string_agg(conrelid::regclass::text, ',' ORDER BY conrelid::regclass::text) FROM pg_constraint
    WHERE contype = 'f' AND confdeltype = 'c' AND connamespace = 'ps'::regnamespace
      AND conrelid::regclass::text NOT IN ('ps.purpose_versions','ps.purpose_legal_grounds','ps.purpose_retention_rules','ps.transfers',
          'ps.activity_data_categories','ps.activity_subject_categories','ps.activity_recipient_categories','ps.activity_systems',
          'ps.activity_measures','ps.activity_engagements','ps.notice_activity_versions')), NULL);
SELECT ps_test.expect_eq('SCHEMA tabelle di dominio accessibili solo al ruolo operativo (nessun privilegio a PUBLIC)',
  (SELECT count(*)::text FROM information_schema.role_table_grants WHERE table_schema = 'ps' AND grantee = 'PUBLIC'), '0');

-- ===================================================================== esito
DO $$
DECLARE tot int; ko int; r record;
BEGIN
  SELECT count(*), count(*) FILTER (WHERE NOT ok) INTO tot, ko FROM ps_test.results;
  FOR r IN SELECT n, label, detail FROM ps_test.results WHERE NOT ok ORDER BY n LOOP
    RAISE NOTICE 'FALLITO #%: % — %', r.n, r.label, r.detail;
  END LOOP;
  RAISE NOTICE 'Test eseguiti: %, riusciti: %, falliti: %', tot, tot - ko, ko;
  IF ko > 0 THEN RAISE EXCEPTION '% test falliti', ko; END IF;
END $$;

ROLLBACK;
