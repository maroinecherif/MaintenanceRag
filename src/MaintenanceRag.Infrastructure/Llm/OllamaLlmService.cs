using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MaintenanceRag.Application.Options;
using MaintenanceRag.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MaintenanceRag.Infrastructure.Llm;

public sealed class OllamaLlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OllamaLlmService> _logger;

    public OllamaLlmService(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaLlmService> logger)
    {
        _httpClient = httpClient;
        _model = options.Value.GenerationModel;
        _logger = logger;
    }

    public async Task<string> GenerateAnswerAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var payload = new { model = _model, prompt, stream = false };

        _logger.LogInformation("LLM – appel Ollama /api/generate (modèle: {Model}).", _model);

        using var response = await _httpClient.PostAsJsonAsync("/api/generate", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
                         cancellationToken: cancellationToken)
                     ?? throw new InvalidOperationException("Ollama /api/generate a retourné une réponse nulle.");

        _logger.LogInformation("LLM – réponse reçue ({Length} caractères).", result.Response.Length);

        return result.Response;
    }

    private sealed record OllamaGenerateResponse(
        [property: JsonPropertyName("response")] string Response
    );
}
