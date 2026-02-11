---
name: run-samples
description: Start the Healthcare Samples stack (Postgres, APIs, Dashboard). Use when asked to run, start, or launch the sample applications.
---

# Run Samples

Start the full Healthcare Samples stack. Decide based on `$ARGUMENTS`:

IMPORTANT: Do NOT run in the background. Run in the foreground so the user can see all output streaming in real-time. Set a long timeout (600000ms).

## Default (no args) - keep existing data

Run with existing database volumes intact:

```bash
cd /Users/christianfindlay/Documents/Code/DataProvider/Samples && ./start.sh
```

## Fresh start - blow away databases

If the user says "fresh", "clean", "reset", or `$ARGUMENTS` contains `--fresh`:

```bash
cd /Users/christianfindlay/Documents/Code/DataProvider/Samples && ./start.sh --fresh
```

## Force rebuild containers

If the user says "rebuild" or `$ARGUMENTS` contains `--build`:

```bash
cd /Users/christianfindlay/Documents/Code/DataProvider/Samples && ./start.sh --build
```

## Both fresh + rebuild

```bash
cd /Users/christianfindlay/Documents/Code/DataProvider/Samples && ./start.sh --fresh --build
```

## Services

| Service | Port |
|---------|------|
| Gatekeeper API | 5002 |
| Clinical API | 5080 |
| Scheduling API | 5001 |
| ICD10 API | 5090 |
| Embedding Service | 8000 |
| Dashboard | 5173 |
| Postgres | 5432 |
