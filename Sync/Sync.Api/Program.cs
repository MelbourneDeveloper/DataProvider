// This file exists ONLY to support WebApplicationFactory<Program> in tests.
// Sync.Api is a LIBRARY providing TOOLS - it does NOT spin up an actual server.
// To run an actual sync server, use these extension methods in your own project.

using Npgsql;
using Sync.Api;

var builder = WebApplication.CreateBuilder(args);

// Configure Npgsql connection pooling for PostgreSQL (if configured)
var pgConnString = builder.Configuration.GetConnectionString("Postgres");
if (!string.IsNullOrEmpty(pgConnString))
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(pgConnString);
    var pgDataSource = dataSourceBuilder.Build();
    builder.Services.AddSingleton(pgDataSource);
}

// Add sync API services using the extension method
builder.Services.AddSyncApiServices(builder.Environment.IsDevelopment());

var app = builder.Build();

// Use rate limiting middleware
app.UseRateLimiter();

// Use request timeout middleware
app.UseSyncRequestTimeout();

// Map all sync endpoints using the extension method
app.MapSyncEndpoints();

app.Run();

/// <summary>
/// Entry point marker for WebApplicationFactory.
/// This class exists ONLY to support test infrastructure.
/// </summary>
public partial class Program { }
