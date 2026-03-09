---
name: container-logs
description: View Docker container logs for the Healthcare Samples stack. Use when asked to check logs, debug container issues, or see service output.
disable-model-invocation: true
allowed-tools: Bash(docker compose *), Bash(docker logs *)
argument-hint: "[container-name] [--tail N]"
---

# Container Logs

View logs from the Healthcare Samples Docker stack.

## Usage

`/container-logs` - show recent logs from all containers
`/container-logs app` - show logs from the app container
`/container-logs db` - show logs from the Postgres container

## Commands

All logs (last 100 lines):
```bash
docker compose -f /Users/christianfindlay/Documents/Code/DataProvider/Samples/docker/docker-compose.yml logs --tail 100
```

Specific container:
```bash
docker compose -f /Users/christianfindlay/Documents/Code/DataProvider/Samples/docker/docker-compose.yml logs --tail 100 $ARGUMENTS
```

Follow logs in real-time (use timeout to avoid hanging):
```bash
timeout 10 docker compose -f /Users/christianfindlay/Documents/Code/DataProvider/Samples/docker/docker-compose.yml logs -f $ARGUMENTS
```

## Container names

| Name | Service |
|------|---------|
| app | All .NET APIs + embedding service |
| db | Postgres 16 + pgvector |
| dashboard | nginx serving static files |

## Check container status

```bash
docker compose -f /Users/christianfindlay/Documents/Code/DataProvider/Samples/docker/docker-compose.yml ps
```
