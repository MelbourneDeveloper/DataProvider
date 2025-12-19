The `Samples/` folder is flat and each entry uses a `Healthcare.<Domain>.<Role>` naming scheme so you can tell the purpose immediately. Everything is FHIR compliant (https://build.fhir.org/resourcelist.html).

Primary clinic scenario
-----------------------
- `Healthcare.PatientService` – SQLite microservice exposing `/fhir/Patient` plus medical-record CRUD; production readic for clinical use. Pure SQL access.
- `Healthcare.AppointmentService` – PostgreSQL scheduling microservice; reads use LQL, writes use SQL, sync powered by `Sync.Postgres`.
- `Healthcare.Sync.http` – HTTP collection that exercises both services (patient intake, appointment booking, and change feed pulls).

Supporting samples
------------------
- `Healthcare.ClinicScheduling` / `.Tests` – pure LQL query sample (no sync) for scheduling dashboards.
- `Healthcare.PatientRecords` / `.Tests` – SQL + DataProvider generator sample demonstrating grouped result models (no sync pipeline).
- `Healthcare.Api` – end-to-end API that precompiles SQL with the generator CLI, showing how to embed generated extensions inside a larger service.
- `Healthcare.InsuranceApi` – mapping/query pack that demonstrates translating between provider/insurer schemas.

Each folder name now communicates the domain (`Healthcare`) and the role (`PatientService`, `AppointmentService`, `ClinicScheduling`, etc.) so you never have to guess whether it’s an API, sync source, or query sandbox.
