-- name: GetChallengeById
SELECT id, user_id, challenge, type, created_at, expires_at
FROM gk_challenge
WHERE id = @id AND expires_at > @now;
