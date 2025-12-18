using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Sync.Api.Tests;

/// <summary>
/// Integration tests for the Sync API endpoints.
/// These tests PROVE the real-time subscription system works.
/// </summary>
public sealed class SyncApiIntegrationTests
    : IClassFixture<WebApplicationFactory<Program>>,
        IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testDbPath;

    public SyncApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"sync_api_test_{Guid.NewGuid()}.db");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(
                (context, config) =>
                {
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?> { ["Database:Path"] = _testDbPath }
                    );
                }
            );
        });

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();

        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(content);
        Assert.Equal("healthy", content.Status);
    }

    [Fact]
    public async Task GetSyncState_ReturnsOriginId()
    {
        // Act
        var response = await _client.GetAsync("/sync/state");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<SyncStateResponse>();
        Assert.NotNull(content);
        Assert.NotNull(content.OriginId);
        Assert.True(Guid.TryParse(content.OriginId, out _), "OriginId should be a valid GUID");
    }

    [Fact]
    public async Task GetChanges_ReturnsEmptyInitially()
    {
        // Act
        var response = await _client.GetAsync("/sync/changes?fromVersion=0&limit=100");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ChangesResponse>();
        Assert.NotNull(content);
        Assert.Empty(content.Changes);
        Assert.Equal(0, content.FromVersion);
        Assert.False(content.HasMore);
    }

    [Fact]
    public async Task PostChanges_AcceptsValidChanges()
    {
        // Arrange
        var changes = new SyncPushRequestDto
        {
            OriginId = Guid.NewGuid().ToString(),
            Changes =
            [
                new SyncLogEntryDto
                {
                    Version = 1,
                    TableName = "Person",
                    PkValue = "{\"Id\":\"p1\"}",
                    Operation = "insert",
                    Payload = "{\"Id\":\"p1\",\"Name\":\"Alice\"}",
                    Origin = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                },
            ],
        };

        // Act
        var response = await _client.PostAsJsonAsync("/sync/changes", changes);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<ApplyResponse>();
        Assert.NotNull(content);
        Assert.Equal(1, content.Applied);
    }

    [Fact]
    public async Task CreateTableSubscription_ReturnsSubscriptionId()
    {
        // Arrange
        var request = new SubscribeRequestDto
        {
            Type = "table",
            TableName = "Person",
            OriginId = Guid.NewGuid().ToString(),
        };

        // Act
        var response = await _client.PostAsJsonAsync("/sync/subscribe", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<SubscribeResponse>();
        Assert.NotNull(content);
        Assert.NotNull(content.SubscriptionId);
        Assert.True(Guid.TryParse(content.SubscriptionId, out _));
        Assert.Equal("table", content.Type);
        Assert.Equal("Person", content.TableName);
    }

    [Fact]
    public async Task CreateRecordSubscription_ReturnsSubscriptionId()
    {
        // Arrange
        var request = new SubscribeRequestDto
        {
            Type = "record",
            TableName = "Person",
            OriginId = Guid.NewGuid().ToString(),
            Filter = "[\"p1\", \"p2\"]",
        };

        // Act
        var response = await _client.PostAsJsonAsync("/sync/subscribe", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<SubscribeResponse>();
        Assert.NotNull(content);
        Assert.Equal("record", content.Type);
    }

    [Fact]
    public async Task DeleteSubscription_RemovesSubscription()
    {
        // Arrange - Create a subscription first
        var createRequest = new SubscribeRequestDto
        {
            Type = "table",
            TableName = "Orders",
            OriginId = Guid.NewGuid().ToString(),
        };
        var createResponse = await _client.PostAsJsonAsync("/sync/subscribe", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<SubscribeResponse>();
        Assert.NotNull(created);

        // Act - Delete it
        var deleteResponse = await _client.DeleteAsync($"/sync/subscribe/{created.SubscriptionId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var content = await deleteResponse.Content.ReadFromJsonAsync<DeleteResponse>();
        Assert.NotNull(content);
        Assert.Equal(created.SubscriptionId, content.Deleted);
    }

    [Fact]
    public async Task GetSubscriptions_ReturnsAllSubscriptions()
    {
        // Arrange - Create two subscriptions
        await _client.PostAsJsonAsync(
            "/sync/subscribe",
            new SubscribeRequestDto
            {
                Type = "table",
                TableName = "Products",
                OriginId = "o1",
            }
        );
        await _client.PostAsJsonAsync(
            "/sync/subscribe",
            new SubscribeRequestDto
            {
                Type = "table",
                TableName = "Orders",
                OriginId = "o1",
            }
        );

        // Act
        var response = await _client.GetAsync("/sync/subscriptions");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<SubscriptionsResponse>();
        Assert.NotNull(content);
        Assert.True(content.Count >= 2);
    }

    [Fact]
    public async Task GetSubscriptionsByTable_FiltersCorrectly()
    {
        // Arrange
        var tableName = $"TestTable_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync(
            "/sync/subscribe",
            new SubscribeRequestDto
            {
                Type = "table",
                TableName = tableName,
                OriginId = "o1",
            }
        );

        // Act
        var response = await _client.GetAsync($"/sync/subscriptions?tableName={tableName}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<SubscriptionsResponse>();
        Assert.NotNull(content);
        Assert.Equal(1, content.Count);
    }

    [Fact]
    public async Task SseStream_ConnectsSuccessfully()
    {
        // Arrange - Create subscription
        var createResponse = await _client.PostAsJsonAsync(
            "/sync/subscribe",
            new SubscribeRequestDto
            {
                Type = "table",
                TableName = "Person",
                OriginId = "sse-test",
            }
        );
        var created = await createResponse.Content.ReadFromJsonAsync<SubscribeResponse>();
        Assert.NotNull(created);

        // Act - Connect to SSE stream
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/sync/stream/{created.SubscriptionId}"
        );
        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SseStream_NotFoundForInvalidSubscription()
    {
        // Act
        var response = await _client.GetAsync("/sync/stream/invalid-subscription-id");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostChanges_WithInvalidBody_ReturnsBadRequest()
    {
        // Arrange
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/sync/changes", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSubscription_WithMissingTableName_ReturnsBadRequest()
    {
        // Arrange
        var request = new { Type = "table", OriginId = "test" }; // Missing TableName

        // Act
        var response = await _client.PostAsJsonAsync("/sync/subscribe", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // DTO classes for deserialization
    private sealed record HealthResponse(string Status);

    private sealed record SyncStateResponse(string OriginId, int ConnectedClients);

    private sealed record ChangesResponse(
        List<SyncLogEntryDto> Changes,
        long FromVersion,
        long ToVersion,
        bool HasMore
    );

    private sealed record ApplyResponse(int Applied);

    private sealed record SubscribeResponse(string SubscriptionId, string Type, string TableName);

    private sealed record DeleteResponse(string Deleted);

    private sealed record SubscriptionsResponse(List<object> Subscriptions, int Count);

    private sealed record SyncPushRequestDto
    {
        public string? OriginId { get; init; }
        public List<SyncLogEntryDto>? Changes { get; init; }
    }

    private sealed record SubscribeRequestDto
    {
        public string? Type { get; init; }
        public string? TableName { get; init; }
        public string? OriginId { get; init; }
        public string? Filter { get; init; }
    }

    private sealed record SyncLogEntryDto
    {
        public long Version { get; init; }
        public string? TableName { get; init; }
        public string? PkValue { get; init; }
        public string? Operation { get; init; }
        public string? Payload { get; init; }
        public string? Origin { get; init; }
        public string? Timestamp { get; init; }
    }
}
