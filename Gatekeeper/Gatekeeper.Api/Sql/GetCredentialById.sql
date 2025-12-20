-- name: GetCredentialById
SELECT c.id, c.user_id, c.public_key, c.sign_count, c.aaguid, c.credential_type, c.transports,
       c.attestation_format, c.created_at, c.last_used_at, c.device_name, c.is_backup_eligible, c.is_backed_up,
       u.display_name, u.email
FROM gk_credential c
JOIN gk_user u ON c.user_id = u.id
WHERE c.id = @id AND u.is_active = 1;
