# Gatekeeper

An independent authentication and authorization microservice: passkey-only authentication (WebAuthn/FIDO2) and fine-grained role-based access control with record-level permissions.

| Project | Description |
|---------|-------------|
| `Gatekeeper.Api` | REST API with WebAuthn and authorization endpoints |
| `Gatekeeper.Migration` | Database schema using DataProvider migrations |
| `Gatekeeper.Api.Tests` | Integration tests |

## Documentation

- Full specification: [docs/specs/gatekeeper-spec.md](../docs/specs/gatekeeper-spec.md)
