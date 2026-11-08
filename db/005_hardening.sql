-- 005_hardening.sql — correzioni emerse dalla revisione del codice (v0.9.0 → v0.9.1).
-- Eseguire come privacy_migrator dopo 004, sia su un database nuovo sia su uno già in uso:
-- le istruzioni sono idempotenti e non toccano i dati esistenti.
BEGIN;
SET LOCAL search_path = ps, pg_catalog;

-- ---------------------------------------------------------------- 1. sequenze e backup
-- pg_dump legge last_value di ogni sequenza: senza USAGE il backup dal ruolo operativo fallisce
-- («permission denied for sequence audit_seq») e non esiste alcuna copia di sicurezza.
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA ps TO privacy_app;
ALTER DEFAULT PRIVILEGES FOR ROLE privacy_migrator IN SCHEMA ps GRANT USAGE, SELECT ON SEQUENCES TO privacy_app;

-- ---------------------------------------------------------------- 2. versioni delle regole di conservazione
-- Senza questo vincolo due bozze della stessa regola potrebbero avere lo stesso numero di versione
-- (e due regole approvate resterebbero attive insieme, duplicando gli elementi scaduti).
CREATE UNIQUE INDEX IF NOT EXISTS ux_retention_policy_version
  ON retention_policy_versions (practice_id, policy_key, version_number);

-- ---------------------------------------------------------------- 3. chiusura degli incidenti
-- Un incidente si chiude solo dopo la valutazione (art. 33) e, una volta chiuso, non si modifica più:
-- altrimenti la data di chiusura e la valutazione potrebbero essere riscritte senza traccia.
CREATE OR REPLACE FUNCTION guard_incident_close() RETURNS trigger
LANGUAGE plpgsql SET search_path = ps, pg_catalog AS $$
BEGIN
  IF OLD.status_code = 'closed' THEN
    IF (to_jsonb(NEW) - 'updated_at' - 'row_version') <> (to_jsonb(OLD) - 'updated_at' - 'row_version') THEN
      RAISE EXCEPTION 'incidente chiuso: non modificabile' USING ERRCODE = 'restrict_violation';
    END IF;
    RETURN NEW;
  END IF;
  IF NEW.status_code = 'closed'
     AND NOT EXISTS (SELECT 1 FROM incident_assessments WHERE incident_id = NEW.id) THEN
    RAISE EXCEPTION 'valutazione mancante: l''incidente si chiude solo dopo la valutazione'
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;

DROP TRIGGER IF EXISTS trg_incident_close ON incidents;
CREATE TRIGGER trg_incident_close BEFORE UPDATE ON incidents
  FOR EACH ROW EXECUTE FUNCTION guard_incident_close();

COMMIT;
