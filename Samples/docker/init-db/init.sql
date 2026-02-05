-- Initialize all databases in single Postgres instance
-- Each database gets its own user with the same password

-- Create databases
CREATE DATABASE gatekeeper;
CREATE DATABASE clinical;
CREATE DATABASE scheduling;
CREATE DATABASE icd10;

-- Create users (all use same password from env var POSTGRES_PASSWORD)
CREATE USER gatekeeper WITH PASSWORD 'changeme';
CREATE USER clinical WITH PASSWORD 'changeme';
CREATE USER scheduling WITH PASSWORD 'changeme';
CREATE USER icd10 WITH PASSWORD 'changeme';

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE gatekeeper TO gatekeeper;
GRANT ALL PRIVILEGES ON DATABASE clinical TO clinical;
GRANT ALL PRIVILEGES ON DATABASE scheduling TO scheduling;
GRANT ALL PRIVILEGES ON DATABASE icd10 TO icd10;

-- Connect to each database and grant schema privileges
\c gatekeeper
GRANT ALL ON SCHEMA public TO gatekeeper;

\c clinical
GRANT ALL ON SCHEMA public TO clinical;

\c scheduling
GRANT ALL ON SCHEMA public TO scheduling;

\c icd10
GRANT ALL ON SCHEMA public TO icd10;
