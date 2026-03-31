---
name: test
description: Run tests for the DataProvider solution or specific test projects. Use when asked to run tests, verify changes, or check test results.
disable-model-invocation: true
allowed-tools: Bash(dotnet test *)
argument-hint: "[component|project-path]"
---

# Test

Run tests for a specific component or the full solution.

## Usage

`/test` - run all tests
`/test dataprovider` - run DataProvider tests
`/test icd10` - run ICD10 API tests

## Test projects by component

| Argument | Test project path |
|----------|------------------|
| dataprovider | DataProvider/DataProvider.Tests |
| dataprovider-example | DataProvider/DataProvider.Example.Tests |
| lql | Lql/Lql.Tests |
| lql-cli | Lql/LqlCli.SQLite.Tests |
| migration | Migration/Migration.Tests |
| sync | Sync/Sync.Tests |
| sync-sqlite | Sync/Sync.SQLite.Tests |
| sync-postgres | Sync/Sync.Postgres.Tests |
| sync-http | Sync/Sync.Http.Tests |
| sync-integration | Sync/Sync.Integration.Tests |
| gatekeeper | Gatekeeper/Gatekeeper.Api.Tests |
| clinical | Samples/Clinical/Clinical.Api.Tests |
| scheduling | Samples/Scheduling/Scheduling.Api.Tests |
| icd10 | Samples/ICD10/ICD10.Api.Tests |
| icd10-cli | Samples/ICD10/ICD10.Cli.Tests |
| dashboard | Samples/Dashboard/Dashboard.Integration.Tests |

## Commands

Run a specific test project:
```bash
dotnet test <project-path> --no-restore --verbosity normal
```

Run all tests in the solution:
```bash
dotnet test /Users/christianfindlay/Documents/Code/DataProvider/DataProvider.sln --no-restore --verbosity normal
```

## Notes

- Tests use xUnit 2.9.2
- Coverage config: `coverlet.runsettings`
- Sync and Gatekeeper tests require a running Postgres instance
- Dashboard tests use Playwright (E2E)
- NEVER skip tests - failing tests are OK, skipped tests are ILLEGAL
