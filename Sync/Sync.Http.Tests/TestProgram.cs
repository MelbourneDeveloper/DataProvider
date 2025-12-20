// Test-only Program entry point for WebApplicationFactory support
// Sync.Http is a LIBRARY - this test project spins up the server for testing.

using Sync.Http;

var options = new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory };

var builder = WebApplication.CreateBuilder(options);

// Add sync API services using the extension method from Sync.Http library
builder.Services.AddSyncApiServices(builder.Environment.IsDevelopment());

var app = builder.Build();

// Use rate limiting middleware
app.UseRateLimiter();

// Use request timeout middleware
app.UseSyncRequestTimeout();

// Map all sync endpoints using the extension method from Sync.Http library
app.MapSyncEndpoints();

app.Run();

/// <summary>
/// Entry point marker for WebApplicationFactory.
/// This class exists ONLY to support test infrastructure.
/// </summary>
public partial class Program { }
