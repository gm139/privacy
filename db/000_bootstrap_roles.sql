-- 000_bootstrap_roles.sql
-- Eseguire UNA VOLTA come superuser (o ruolo con CREATEROLE) sul database di destinazione.
-- Le password NON sono qui: l'installer le imposta con ALTER ROLE ... PASSWORD.
--   privacy_migrator : proprietario dello schema, usato solo da installer/aggiornamenti (DDL).
--   privacy_app      : ruolo operativo dell'applicazione (DML limitato, nessun DDL).
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'privacy_migrator') THEN
    CREATE ROLE privacy_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'privacy_app') THEN
    CREATE ROLE privacy_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE;
  END IF;
  EXECUTE format('GRANT CONNECT, CREATE ON DATABASE %I TO privacy_migrator', current_database());
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO privacy_app', current_database());
  EXECUTE 'ALTER ROLE privacy_app SET search_path = ps, pg_catalog';
  EXECUTE 'ALTER ROLE privacy_migrator SET search_path = ps, pg_catalog';
END $$;
