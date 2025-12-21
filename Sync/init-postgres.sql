-- Initialize Postgres with sync schema
-- This is the Postgres equivalent of the SQLite sync schema

-- Sync state (persistent)
CREATE TABLE IF NOT EXISTS _sync_state (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- Sync session (ephemeral flag for trigger suppression)
CREATE TABLE IF NOT EXISTS _sync_session (
    sync_active INTEGER DEFAULT 0
);
INSERT INTO _sync_session VALUES (0) ON CONFLICT DO NOTHING;

-- Change log
CREATE TABLE IF NOT EXISTS _sync_log (
    version BIGSERIAL PRIMARY KEY,
    table_name TEXT NOT NULL,
    pk_value TEXT NOT NULL,
    operation TEXT NOT NULL CHECK (operation IN ('insert', 'update', 'delete')),
    payload TEXT,
    origin TEXT NOT NULL,
    timestamp TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_sync_log_version ON _sync_log(version);
CREATE INDEX IF NOT EXISTS idx_sync_log_table ON _sync_log(table_name, version);

-- Client tracking for tombstone retention
CREATE TABLE IF NOT EXISTS _sync_clients (
    origin_id TEXT PRIMARY KEY,
    last_sync_version BIGINT NOT NULL DEFAULT 0,
    last_sync_timestamp TEXT NOT NULL,
    created_at TEXT NOT NULL
);

-- Subscriptions
CREATE TABLE IF NOT EXISTS _sync_subscriptions (
    subscription_id TEXT PRIMARY KEY,
    origin_id TEXT NOT NULL,
    subscription_type TEXT NOT NULL CHECK (subscription_type IN ('record', 'table', 'query')),
    table_name TEXT NOT NULL,
    filter TEXT,
    created_at TEXT NOT NULL,
    expires_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_subscriptions_table ON _sync_subscriptions(table_name);
CREATE INDEX IF NOT EXISTS idx_subscriptions_origin ON _sync_subscriptions(origin_id);

-- Initialize origin_id and sync state
INSERT INTO _sync_state (key, value) VALUES ('origin_id', '') ON CONFLICT (key) DO NOTHING;
INSERT INTO _sync_state (key, value) VALUES ('last_server_version', '0') ON CONFLICT (key) DO NOTHING;
INSERT INTO _sync_state (key, value) VALUES ('last_push_version', '0') ON CONFLICT (key) DO NOTHING;

-- Sample table for testing
CREATE TABLE IF NOT EXISTS Person (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Email TEXT
);

-- Create trigger function for Person table
CREATE OR REPLACE FUNCTION person_sync_trigger() RETURNS TRIGGER AS $$
BEGIN
    IF (SELECT sync_active FROM _sync_session LIMIT 1) = 0 THEN
        IF TG_OP = 'INSERT' THEN
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES (
                'Person',
                jsonb_build_object('Id', NEW.Id)::text,
                'insert',
                jsonb_build_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email)::text,
                (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
            );
        ELSIF TG_OP = 'UPDATE' THEN
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES (
                'Person',
                jsonb_build_object('Id', NEW.Id)::text,
                'update',
                jsonb_build_object('Id', NEW.Id, 'Name', NEW.Name, 'Email', NEW.Email)::text,
                (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
            );
        ELSIF TG_OP = 'DELETE' THEN
            INSERT INTO _sync_log (table_name, pk_value, operation, payload, origin, timestamp)
            VALUES (
                'Person',
                jsonb_build_object('Id', OLD.Id)::text,
                'delete',
                NULL,
                (SELECT value FROM _sync_state WHERE key = 'origin_id'),
                to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
            );
        END IF;
    END IF;
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

-- Create triggers
DROP TRIGGER IF EXISTS person_sync_insert ON Person;
DROP TRIGGER IF EXISTS person_sync_update ON Person;
DROP TRIGGER IF EXISTS person_sync_delete ON Person;

CREATE TRIGGER person_sync_insert AFTER INSERT ON Person FOR EACH ROW EXECUTE FUNCTION person_sync_trigger();
CREATE TRIGGER person_sync_update AFTER UPDATE ON Person FOR EACH ROW EXECUTE FUNCTION person_sync_trigger();
CREATE TRIGGER person_sync_delete AFTER DELETE ON Person FOR EACH ROW EXECUTE FUNCTION person_sync_trigger();
