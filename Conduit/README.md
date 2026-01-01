# Conduit

A functional, MediatR-style request/response pipeline for .NET with built-in behaviors, validation, and notifications. Zero dependencies on DI containers, fully immutable, and designed for explicit error handling.

## Overview

Conduit provides:

- **Request/Response Pattern** - Send requests through a pipeline to registered handlers
- **Notification Pattern** - Publish notifications to multiple handlers (fan-out)
- **Behavior Pipeline** - Composable middleware for cross-cutting concerns
- **Built-in Behaviors** - Logging, timeout, validation, retry, exception handling
- **Explicit Error Handling** - All operations return `Result<T, ConduitError>`, never throw
- **Fully Immutable** - Registry and context are immutable records
- **No Interfaces** - Uses delegates (`Func<T>`, `Action<T>`) per functional programming style

## Installation

```xml
<PackageReference Include="Conduit" Version="1.0.0" />
```

## Quick Start

### 1. Define Request and Response Types

```csharp
// Requests and responses are records (immutable)
public sealed record GetUserRequest(int UserId);
public sealed record GetUserResponse(string Name, string Email);
```

### 2. Create a Handler

```csharp
// Define type alias for cleaner code
using GetUserResult = Result<GetUserResponse, ConduitError>;

// Handlers are static methods that return Result<TResponse, ConduitError>
public static class UserHandlers
{
    public static Task<GetUserResult> Handle(
        GetUserRequest request,
        CancellationToken ct
    ) =>
        Task.FromResult<GetUserResult>(
            new GetUserResult.Ok(new GetUserResponse("Alice", "alice@example.com"))
        );
}
```

### 3. Build the Registry

```csharp
using Conduit;

// Registry is immutable - each Add returns a new registry
var registry = ConduitRegistry.Empty
    .AddHandler<GetUserRequest, GetUserResponse>(UserHandlers.Handle);
```

### 4. Send Requests

```csharp
using Microsoft.Extensions.Logging;

var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Conduit");

var result = await Dispatcher.Send<GetUserRequest, GetUserResponse>(
    request: new GetUserRequest(UserId: 42),
    registry: registry,
    logger: logger
);

// Pattern match on result - no exceptions!
var message = result switch
{
    GetUserResult.Ok ok => $"User: {ok.Value.Name}",
    GetUserResult.Error err => $"Error: {err.Value}",
};
```

## Behaviors (Middleware)

Behaviors wrap handler execution for cross-cutting concerns. They execute in order (first registered = outermost).

### Adding Behaviors

```csharp
var registry = ConduitRegistry.Empty
    .AddHandler<GetUserRequest, GetUserResponse>(UserHandlers.Handle)
    .AddBehavior(BuiltInBehaviors.Logging<GetUserRequest, GetUserResponse>())
    .AddBehavior(BuiltInBehaviors.Timeout<GetUserRequest, GetUserResponse>(
        TimeSpan.FromSeconds(30)))
    .AddBehavior(BuiltInBehaviors.ExceptionHandler<GetUserRequest, GetUserResponse>());
```

### Built-in Behaviors

| Behavior | Description |
|----------|-------------|
| `Logging<TReq, TRes>()` | Logs request start, success/failure, and elapsed time |
| `Timeout<TReq, TRes>(TimeSpan)` | Cancels requests exceeding timeout |
| `Validation<TReq, TRes>(Func<TReq, ImmutableList<string>>)` | Validates request before handler |
| `Retry<TReq, TRes>(maxRetries, baseDelayMs, shouldRetry)` | Retries with exponential backoff |
| `ExceptionHandler<TReq, TRes>()` | Converts exceptions to `ConduitError` |

### Custom Behaviors

```csharp
using GetUserResult = Result<GetUserResponse, ConduitError>;

BehaviorHandler<GetUserRequest, GetUserResponse> authBehavior =
    async (context, next, logger, ct) =>
    {
        // Before handler
        logger.LogInformation("Checking auth for correlation {Id}", context.CorrelationId);

        if (!IsAuthorized(context.Request))
        {
            return new GetUserResult.Error(
                new ConduitErrorBehaviorRejection("Auth", "Unauthorized")
            );
        }

        // Call next in pipeline
        var result = await next(context, ct);

        // After handler (optional)
        return result;
    };

var registry = ConduitRegistry.Empty
    .AddHandler<GetUserRequest, GetUserResponse>(UserHandlers.Handle)
    .AddBehavior(authBehavior);
```

## Validation

Register validators that run before the handler:

```csharp
var registry = ConduitRegistry.Empty
    .AddHandler<GetUserRequest, GetUserResponse>(UserHandlers.Handle)
    .AddValidator<GetUserRequest>(request =>
        request.UserId <= 0
            ? ["UserId must be positive"]
            : []);
```

Validation errors return `ConduitErrorValidation` with the error list.

## Notifications

Notifications fan out to multiple handlers. Handlers run in parallel, and errors don't stop other handlers.

```csharp
// Define notification
public sealed record UserCreatedNotification(int UserId, string Email);

using UnitResult = Result<Unit, ConduitError>;

// Register handlers
var registry = ConduitRegistry.Empty
    .AddNotificationHandler<UserCreatedNotification>(async (notification, ct) =>
    {
        await SendWelcomeEmail(notification.Email);
        return new UnitResult.Ok(Unit.Value);
    })
    .AddNotificationHandler<UserCreatedNotification>(async (notification, ct) =>
    {
        await LogAuditEvent(notification.UserId);
        return new UnitResult.Ok(Unit.Value);
    });

// Publish
var result = await Dispatcher.Publish(
    notification: new UserCreatedNotification(42, "alice@example.com"),
    registry: registry,
    logger: logger
);

using NotifyResult = Result<NotificationResult, ConduitError>;

// Result contains handler count, success count, and any errors
if (result is NotifyResult.Ok ok)
{
    Console.WriteLine($"Published to {ok.Value.HandlerCount} handlers, {ok.Value.SuccessCount} succeeded");
}
```

## Pipeline Context

Behaviors receive a `PipelineContext` with metadata:

```csharp
public sealed record PipelineContext<TRequest, TResponse>(
    TRequest Request,           // The request being processed
    string RequestType,         // Full type name
    DateTimeOffset StartTime,   // When processing started
    string CorrelationId,       // Unique ID for tracing
    ImmutableDictionary<string, object> Properties  // Custom properties
);
```

Add properties for behaviors to share:

```csharp
var contextWithTenant = PipelineContext.WithProperty(context, "TenantId", 123);
var tenantId = PipelineContext.GetProperty<TReq, TRes, int>(context, "TenantId");
```

## Error Types

All errors derive from `ConduitError` (closed hierarchy):

| Error Type | Description |
|------------|-------------|
| `ConduitErrorNoHandler` | No handler registered for request type |
| `ConduitErrorHandlerFailed` | Handler threw an exception |
| `ConduitErrorValidation` | Validation failed (contains error list) |
| `ConduitErrorBehaviorRejection` | Behavior rejected the request |
| `ConduitErrorTimeout` | Request exceeded timeout |
| `ConduitErrorNotificationHandler` | Notification handler failed (non-fatal) |

## Dependency Injection

Optional integration with `IServiceCollection`:

```csharp
services.AddConduit(registry => registry
    .AddHandler<GetUserRequest, GetUserResponse>(UserHandlers.Handle)
    .AddBehavior(BuiltInBehaviors.Logging<GetUserRequest, GetUserResponse>())
);

// Inject ConduitRegistry where needed
public class MyService(ConduitRegistry registry, ILogger<MyService> logger)
{
    public Task<Result<GetUserResponse, ConduitError>> GetUser(int id) =>
        Dispatcher.Send<GetUserRequest, GetUserResponse>(
            new GetUserRequest(id), registry, logger);
}
```

## Design Principles

Conduit follows the coding rules from `CLAUDE.md`:

- **No exceptions** - All operations return `Result<T, ConduitError>`
- **No classes** - Uses records and static methods (FP style)
- **No interfaces** - Uses delegates for abstractions
- **Immutable** - Registry and context are immutable records
- **Pattern matching** - Switch expressions on result types
- **Named parameters** - All calls use named parameters

## Projects

| Project | Description |
|---------|-------------|
| `Conduit` | Core library |
| `Conduit.Tests` | E2E tests (no mocking) |

## License

See repository root for license information.
