using Migration;
using static Migration.PortableTypes;

namespace Gatekeeper.Migration;

/// <summary>
/// Database schema for Gatekeeper authentication and authorization service.
/// </summary>
public static class GatekeeperSchema
{
    /// <summary>
    /// Builds the complete Gatekeeper schema definition.
    /// </summary>
    public static SchemaDefinition Build() =>
        Schema
            .Define("gatekeeper")
            // ═══════════════════════════════════════════════════════════════
            // CORE AUTHENTICATION TABLES
            // ═══════════════════════════════════════════════════════════════

            // Users (minimal - no password!)
            .Table(
                "gk_user",
                t =>
                    t.Column("id", Text, c => c.PrimaryKey())
                        .Column("display_name", Text, c => c.NotNull())
                        .Column("email", Text)
                        .Column("created_at", Text, c => c.NotNull())
                        .Column("last_login_at", Text)
                        .Column("is_active", PortableTypes.Boolean, c => c.NotNull().Default("1"))
                        .Column("metadata", Json)
                        .Index("idx_user_email", "email", unique: true)
            )
            // WebAuthn Credentials (passkeys)
            .Table(
                "gk_credential",
                t =>
                    t.Column("id", Text, c => c.PrimaryKey())
                        .Column("user_id", Text, c => c.NotNull())
                        .Column("public_key", Blob, c => c.NotNull())
                        .Column("sign_count", Int, c => c.NotNull().Default("0"))
                        .Column("aaguid", Text)
                        .Column("credential_type", Text, c => c.NotNull())
                        .Column("transports", Json)
                        .Column("attestation_format", Text)
                        .Column("created_at", Text, c => c.NotNull())
                        .Column("last_used_at", Text)
                        .Column("device_name", Text)
                        .Column("is_backup_eligible", PortableTypes.Boolean)
                        .Column("is_backed_up", PortableTypes.Boolean)
                        .ForeignKey("user_id", "gk_user", "id", onDelete: ForeignKeyAction.Cascade)
                        .Index("idx_credential_user", "user_id")
            )
            // Sessions (stateful for revocation support)
            .Table(
                "gk_session",
                t =>
                    t.Column("id", Text, c => c.PrimaryKey())
                        .Column("user_id", Text, c => c.NotNull())
                        .Column("credential_id", Text)
                        .Column("created_at", Text, c => c.NotNull())
                        .Column("expires_at", Text, c => c.NotNull())
                        .Column("last_activity_at", Text, c => c.NotNull())
                        .Column("ip_address", Text)
                        .Column("user_agent", Text)
                        .Column("is_revoked", PortableTypes.Boolean, c => c.NotNull().Default("0"))
                        .ForeignKey("user_id", "gk_user", "id", onDelete: ForeignKeyAction.Cascade)
                        .ForeignKey("credential_id", "gk_credential", "id")
                        .Index("idx_session_user", "user_id")
                        .Index("idx_session_expires", "expires_at")
            )
            // WebAuthn Challenge Store (temporary, for registration/login)
            .Table(
                "gk_challenge",
                t =>
                    t.Column("id", Text, c => c.PrimaryKey())
                        .Column("user_id", Text)
                        .Column("challenge", Blob, c => c.NotNull())
                        .Column("type", Text, c => c.NotNull())
                        .Column("created_at", Text, c => c.NotNull())
                        .Column("expires_at", Text, c => c.NotNull())
            )
            // ═══════════════════════════════════════════════════════════════
            // RBAC TABLES
            // ═══════════════════════════════════════════════════════════════

            // Roles
            .Table(
                "gk_role",
                t =>
                    t.Column("id", Text, c => c.PrimaryKey())
                        .Column("name", Text, c => c.NotNull())
                        .Column("description", Text)
                        .Column("is_system", PortableTypes.Boolean, c => c.NotNull().Default("0"))
                        .Column("created_at", Text, c => c.NotNull())
                        .Column("parent_role_id", Text)
                        .ForeignKey("parent_role_id", "gk_role", "id")
                        .Index("idx_role_name", "name", unique: true)
            )
            // User-Role assignments
            .Table(
                "gk_user_role",
                t =>
                    t.Column("user_id", Text, c => c.NotNull())
                        .Column("role_id", Text, c => c.NotNull())
                        .Column("granted_at", Text, c => c.NotNull())
                        .Column("granted_by", Text)
                        .Column("expires_at", Text)
                        .CompositePrimaryKey("user_id", "role_id")
                        .ForeignKey("user_id", "gk_user", "id", onDelete: ForeignKeyAction.Cascade)
                        .ForeignKey("role_id", "gk_role", "id", onDelete: ForeignKeyAction.Cascade)
                        .ForeignKey("granted_by", "gk_user", "id")
            )
            // Permissions (the actual capabilities)
            .Table(
                "gk_permission",
                t =>
                    t.Column("id", Text, c => c.PrimaryKey())
                        .Column("code", Text, c => c.NotNull())
                        .Column("resource_type", Text, c => c.NotNull())
                        .Column("action", Text, c => c.NotNull())
                        .Column("description", Text)
                        .Column("created_at", Text, c => c.NotNull())
                        .Index("idx_permission_code", "code", unique: true)
                        .Index("idx_permission_resource", "resource_type")
            )
            // Role-Permission assignments
            .Table(
                "gk_role_permission",
                t =>
                    t.Column("role_id", Text, c => c.NotNull())
                        .Column("permission_id", Text, c => c.NotNull())
                        .Column("granted_at", Text, c => c.NotNull())
                        .CompositePrimaryKey("role_id", "permission_id")
                        .ForeignKey("role_id", "gk_role", "id", onDelete: ForeignKeyAction.Cascade)
                        .ForeignKey(
                            "permission_id",
                            "gk_permission",
                            "id",
                            onDelete: ForeignKeyAction.Cascade
                        )
            )
            // Direct user-permission grants (bypass roles for exceptions)
            .Table(
                "gk_user_permission",
                t =>
                    t.Column("user_id", Text, c => c.NotNull())
                        .Column("permission_id", Text, c => c.NotNull())
                        .Column("scope_type", Text)
                        .Column("scope_value", Text)
                        .Column("granted_at", Text, c => c.NotNull())
                        .Column("granted_by", Text)
                        .Column("expires_at", Text)
                        .Column("reason", Text)
                        .ForeignKey("user_id", "gk_user", "id", onDelete: ForeignKeyAction.Cascade)
                        .ForeignKey(
                            "permission_id",
                            "gk_permission",
                            "id",
                            onDelete: ForeignKeyAction.Cascade
                        )
                        .ForeignKey("granted_by", "gk_user", "id")
                        .Index(
                            "idx_user_permission",
                            ["user_id", "permission_id", "scope_value"],
                            unique: true
                        )
            )
            // ═══════════════════════════════════════════════════════════════
            // FINE-GRAINED ACCESS CONTROL
            // ═══════════════════════════════════════════════════════════════

            // Resource-level permissions (record-level access)
            .Table(
                "gk_resource_grant",
                t =>
                    t.Column("id", Text, c => c.PrimaryKey())
                        .Column("user_id", Text, c => c.NotNull())
                        .Column("resource_type", Text, c => c.NotNull())
                        .Column("resource_id", Text, c => c.NotNull())
                        .Column("permission_id", Text, c => c.NotNull())
                        .Column("granted_at", Text, c => c.NotNull())
                        .Column("granted_by", Text)
                        .Column("expires_at", Text)
                        .ForeignKey("user_id", "gk_user", "id", onDelete: ForeignKeyAction.Cascade)
                        .ForeignKey("permission_id", "gk_permission", "id")
                        .ForeignKey("granted_by", "gk_user", "id")
                        .Index("idx_resource_grant_user", "user_id")
                        .Index("idx_resource_grant_resource", ["resource_type", "resource_id"])
                        .Unique(
                            "uq_resource_grant",
                            "user_id",
                            "resource_type",
                            "resource_id",
                            "permission_id"
                        )
            )
            // Policies (conditional access rules)
            .Table(
                "gk_policy",
                t =>
                    t.Column("id", Text, c => c.PrimaryKey())
                        .Column("name", Text, c => c.NotNull())
                        .Column("description", Text)
                        .Column("resource_type", Text, c => c.NotNull())
                        .Column("action", Text, c => c.NotNull())
                        .Column("condition", Json, c => c.NotNull())
                        .Column("effect", Text, c => c.NotNull().Default("'allow'"))
                        .Column("priority", Int, c => c.NotNull().Default("0"))
                        .Column("is_active", PortableTypes.Boolean, c => c.NotNull().Default("1"))
                        .Column("created_at", Text, c => c.NotNull())
                        .Index("idx_policy_name", "name", unique: true)
            )
            .Build();
}
