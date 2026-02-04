using Microsoft.AspNetCore.Mvc.Testing;

namespace ICD10.Api.Tests;

/// <summary>
/// WebApplicationFactory for ICD10.Api e2e testing.
/// </summary>
public sealed class ICD10ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Checks if the embedding service at localhost:8000 is available.
    /// </summary>
    public bool EmbeddingServiceAvailable
    {
        get
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var response = client.GetAsync("http://localhost:8000/health").Result;
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
