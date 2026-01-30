namespace ICD10AM.Cli.Tests;

/// <summary>
/// Mock HTTP handler for embedding service responses.
/// </summary>
sealed class MockEmbeddingHandler : HttpMessageHandler
{
    readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    /// <summary>
    /// Creates a handler with a custom response function.
    /// </summary>
    public MockEmbeddingHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
        _handler = handler;

    /// <summary>
    /// Creates a handler that returns successful embedding responses.
    /// </summary>
    public static MockEmbeddingHandler Success(int embeddingDim = 384) =>
        new(request =>
        {
            var embedding = CreateFakeEmbedding(embeddingDim, DateTime.UtcNow.Ticks.GetHashCode());
            var json = JsonSerializer.Serialize(new { embedding });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            };
        });

    /// <summary>
    /// Creates a handler that returns error responses.
    /// </summary>
    public static MockEmbeddingHandler Error(
        HttpStatusCode statusCode = HttpStatusCode.InternalServerError
    ) => new(_ => new HttpResponseMessage(statusCode));

    /// <summary>
    /// Creates a handler that throws exceptions.
    /// </summary>
    public static MockEmbeddingHandler Throws(Exception exception) => new(_ => throw exception);

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_handler(request));
    }

    static float[] CreateFakeEmbedding(int dim, int seed)
    {
        var rng = new Random(seed);
        var embedding = new float[dim];
        for (var i = 0; i < dim; i++)
        {
            embedding[i] = (float)(rng.NextDouble() * 2 - 1);
        }

        // Normalize
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        for (var i = 0; i < dim; i++)
        {
            embedding[i] /= (float)magnitude;
        }

        return embedding;
    }
}
