using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MaintenanceRag.Application.Options;
using MaintenanceRag.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MaintenanceRag.Infrastructure.Embeddings;

public sealed class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _model = options.Value.EmbeddingModel;
        _logger = logger;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Generating embedding for text (length={Length}).", text.Length);

        var payload = new { model = _model, input = text };

        using var response = await _httpClient.PostAsJsonAsync("/api/embed", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Ollama returned a null response.");

        if (result.Embeddings is null || result.Embeddings.Length == 0)
            throw new InvalidOperationException("Ollama returned empty embeddings.");

        return result.Embeddings[0];
    }

    private sealed record OllamaEmbedResponse(
        [property: JsonPropertyName("embeddings")] float[][] Embeddings
    );
}
