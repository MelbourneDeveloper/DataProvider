-- name: GetCredentialsByUserId
SELECT id, user_id, public_key, sign_count, aaguid, credential_type, transports,
       attestation_format, created_at, last_used_at, device_name,
       is_backup_eligible, is_backed_up
FROM gk_credential
WHERE user_id = @userId;
