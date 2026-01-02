---
layout: layouts/docs.njk
title: Gatekeeper
description: WebAuthn authentication and RBAC for your APIs.
---

## Overview

Gatekeeper provides WebAuthn-based authentication and Role-Based Access Control (RBAC) for your DataProvider APIs.

## Features

- **WebAuthn**: Passwordless authentication using FIDO2
- **RBAC**: Role-based access control
- **API Security**: Protect your data endpoints
- **Integration**: Works seamlessly with DataProvider

## Setup

```csharp
services.AddGatekeeper(options =>
{
    options.RelyingPartyId = "your-domain.com";
    options.RelyingPartyName = "Your App";
});
```

## Protecting Endpoints

```csharp
[Authorize(Roles = "Admin")]
public async Task<IActionResult> GetSecureData()
{
    // Protected endpoint
}
```

## Next Steps

- [DataProvider Documentation](/docs/dataprovider/)
- [Sync Documentation](/docs/sync/)
