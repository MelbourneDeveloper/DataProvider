#!/bin/bash
set -e

# Create databases and users using environment variable for password
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE DATABASE gatekeeper;
    CREATE DATABASE clinical;
    CREATE DATABASE scheduling;
    CREATE DATABASE icd10;

    CREATE USER gatekeeper WITH PASSWORD '$POSTGRES_PASSWORD';
    CREATE USER clinical WITH PASSWORD '$POSTGRES_PASSWORD';
    CREATE USER scheduling WITH PASSWORD '$POSTGRES_PASSWORD';
    CREATE USER icd10 WITH PASSWORD '$POSTGRES_PASSWORD';

    GRANT ALL PRIVILEGES ON DATABASE gatekeeper TO gatekeeper;
    GRANT ALL PRIVILEGES ON DATABASE clinical TO clinical;
    GRANT ALL PRIVILEGES ON DATABASE scheduling TO scheduling;
    GRANT ALL PRIVILEGES ON DATABASE icd10 TO icd10;
EOSQL

# Grant schema privileges
for db in gatekeeper clinical scheduling icd10; do
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$db" <<-EOSQL
        GRANT ALL ON SCHEMA public TO $db;
EOSQL
done

# Enable pgvector extension for ICD10 database (vector similarity search)
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "icd10" <<-EOSQL
    CREATE EXTENSION IF NOT EXISTS vector;
EOSQL
