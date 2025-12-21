-- Gatekeeper Schema for DataProvider source generation
-- Generated from Gatekeeper.Migration.GatekeeperSchema

-- Core Authentication Tables

CREATE TABLE IF NOT EXISTS [gk_user] (
    [id] TEXT NOT NULL PRIMARY KEY,
    [display_name] TEXT NOT NULL,
    [email] TEXT,
    [created_at] TEXT NOT NULL,
    [last_login_at] TEXT,
    [is_active] INTEGER NOT NULL DEFAULT 1,
    [metadata] TEXT
);
CREATE UNIQUE INDEX IF NOT EXISTS [idx_user_email] ON [gk_user] ([email]);

CREATE TABLE IF NOT EXISTS [gk_credential] (
    [id] TEXT NOT NULL PRIMARY KEY,
    [user_id] TEXT NOT NULL,
    [public_key] BLOB NOT NULL,
    [sign_count] INTEGER NOT NULL DEFAULT 0,
    [aaguid] TEXT,
    [credential_type] TEXT NOT NULL,
    [transports] TEXT,
    [attestation_format] TEXT,
    [created_at] TEXT NOT NULL,
    [last_used_at] TEXT,
    [device_name] TEXT,
    [is_backup_eligible] INTEGER,
    [is_backed_up] INTEGER,
    FOREIGN KEY ([user_id]) REFERENCES [gk_user] ([id]) ON DELETE CASCADE ON UPDATE NO ACTION
);
CREATE INDEX IF NOT EXISTS [idx_credential_user] ON [gk_credential] ([user_id]);

CREATE TABLE IF NOT EXISTS [gk_session] (
    [id] TEXT NOT NULL PRIMARY KEY,
    [user_id] TEXT NOT NULL,
    [credential_id] TEXT,
    [created_at] TEXT NOT NULL,
    [expires_at] TEXT NOT NULL,
    [last_activity_at] TEXT NOT NULL,
    [ip_address] TEXT,
    [user_agent] TEXT,
    [is_revoked] INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY ([user_id]) REFERENCES [gk_user] ([id]) ON DELETE CASCADE ON UPDATE NO ACTION,
    FOREIGN KEY ([credential_id]) REFERENCES [gk_credential] ([id]) ON DELETE NO ACTION ON UPDATE NO ACTION
);
CREATE INDEX IF NOT EXISTS [idx_session_user] ON [gk_session] ([user_id]);
CREATE INDEX IF NOT EXISTS [idx_session_expires] ON [gk_session] ([expires_at]);

CREATE TABLE IF NOT EXISTS [gk_challenge] (
    [id] TEXT NOT NULL PRIMARY KEY,
    [user_id] TEXT,
    [challenge] BLOB NOT NULL,
    [type] TEXT NOT NULL,
    [created_at] TEXT NOT NULL,
    [expires_at] TEXT NOT NULL
);

-- RBAC Tables

CREATE TABLE IF NOT EXISTS [gk_role] (
    [id] TEXT NOT NULL PRIMARY KEY,
    [name] TEXT NOT NULL,
    [description] TEXT,
    [is_system] INTEGER NOT NULL DEFAULT 0,
    [created_at] TEXT NOT NULL,
    [parent_role_id] TEXT,
    FOREIGN KEY ([parent_role_id]) REFERENCES [gk_role] ([id]) ON DELETE NO ACTION ON UPDATE NO ACTION
);
CREATE UNIQUE INDEX IF NOT EXISTS [idx_role_name] ON [gk_role] ([name]);

CREATE TABLE IF NOT EXISTS [gk_user_role] (
    [user_id] TEXT NOT NULL,
    [role_id] TEXT NOT NULL,
    [granted_at] TEXT NOT NULL,
    [granted_by] TEXT,
    [expires_at] TEXT,
    PRIMARY KEY ([user_id], [role_id]),
    FOREIGN KEY ([user_id]) REFERENCES [gk_user] ([id]) ON DELETE CASCADE ON UPDATE NO ACTION,
    FOREIGN KEY ([role_id]) REFERENCES [gk_role] ([id]) ON DELETE CASCADE ON UPDATE NO ACTION,
    FOREIGN KEY ([granted_by]) REFERENCES [gk_user] ([id]) ON DELETE NO ACTION ON UPDATE NO ACTION
);

CREATE TABLE IF NOT EXISTS [gk_permission] (
    [id] TEXT NOT NULL PRIMARY KEY,
    [code] TEXT NOT NULL,
    [resource_type] TEXT NOT NULL,
    [action] TEXT NOT NULL,
    [description] TEXT,
    [created_at] TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS [idx_permission_code] ON [gk_permission] ([code]);
CREATE INDEX IF NOT EXISTS [idx_permission_resource] ON [gk_permission] ([resource_type]);

CREATE TABLE IF NOT EXISTS [gk_role_permission] (
    [role_id] TEXT NOT NULL,
    [permission_id] TEXT NOT NULL,
    [granted_at] TEXT NOT NULL,
    PRIMARY KEY ([role_id], [permission_id]),
    FOREIGN KEY ([role_id]) REFERENCES [gk_role] ([id]) ON DELETE CASCADE ON UPDATE NO ACTION,
    FOREIGN KEY ([permission_id]) REFERENCES [gk_permission] ([id]) ON DELETE CASCADE ON UPDATE NO ACTION
);

CREATE TABLE IF NOT EXISTS [gk_user_permission] (
    [user_id] TEXT NOT NULL,
    [permission_id] TEXT NOT NULL,
    [scope_type] TEXT,
    [scope_value] TEXT,
    [granted_at] TEXT NOT NULL,
    [granted_by] TEXT,
    [expires_at] TEXT,
    [reason] TEXT,
    FOREIGN KEY ([user_id]) REFERENCES [gk_user] ([id]) ON DELETE CASCADE ON UPDATE NO ACTION,
    FOREIGN KEY ([permission_id]) REFERENCES [gk_permission] ([id]) ON DELETE CASCADE ON UPDATE NO ACTION,
    FOREIGN KEY ([granted_by]) REFERENCES [gk_user] ([id]) ON DELETE NO ACTION ON UPDATE NO ACTION
);
CREATE UNIQUE INDEX IF NOT EXISTS [idx_user_permission] ON [gk_user_permission] ([user_id], [permission_id], [scope_value]);

-- Fine-Grained Access Control

CREATE TABLE IF NOT EXISTS [gk_resource_grant] (
    [id] TEXT NOT NULL PRIMARY KEY,
    [user_id] TEXT NOT NULL,
    [resource_type] TEXT NOT NULL,
    [resource_id] TEXT NOT NULL,
    [permission_id] TEXT NOT NULL,
    [granted_at] TEXT NOT NULL,
    [granted_by] TEXT,
    [expires_at] TEXT,
    FOREIGN KEY ([user_id]) REFERENCES [gk_user] ([id]) ON DELETE CASCADE ON UPDATE NO ACTION,
    FOREIGN KEY ([permission_id]) REFERENCES [gk_permission] ([id]) ON DELETE NO ACTION ON UPDATE NO ACTION,
    FOREIGN KEY ([granted_by]) REFERENCES [gk_user] ([id]) ON DELETE NO ACTION ON UPDATE NO ACTION,
    UNIQUE ([user_id], [resource_type], [resource_id], [permission_id])
);
CREATE INDEX IF NOT EXISTS [idx_resource_grant_user] ON [gk_resource_grant] ([user_id]);
CREATE INDEX IF NOT EXISTS [idx_resource_grant_resource] ON [gk_resource_grant] ([resource_type], [resource_id]);

CREATE TABLE IF NOT EXISTS [gk_policy] (
    [id] TEXT NOT NULL PRIMARY KEY,
    [name] TEXT NOT NULL,
    [description] TEXT,
    [resource_type] TEXT NOT NULL,
    [action] TEXT NOT NULL,
    [condition] TEXT NOT NULL,
    [effect] TEXT NOT NULL DEFAULT 'allow',
    [priority] INTEGER NOT NULL DEFAULT 0,
    [is_active] INTEGER NOT NULL DEFAULT 1,
    [created_at] TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS [idx_policy_name] ON [gk_policy] ([name]);
