using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Dapper;
using MaintenanceRag.Application.DTOs;
using MaintenanceRag.Application.Services;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MaintenanceRag.Infrastructure.Services;

public sealed class RagService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly HttpClient _httpClient;
    private readonly string _connectionString;
    private readonly string _generationModel;
    private readonly ILogger<RagService> _logger;

    public RagService(
        IEmbeddingService embeddingService,
        HttpClient httpClient,
        string connectionString,
        string generationModel,
        ILogger<RagService> logger)
    {
        _embeddingService = embeddingService;
        _httpClient = httpClient;
        _connectionString = connectionString;
        _generationModel = generationModel;
        _logger = logger;
    }

    public async Task<AskResponse> AskAsync(AskRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("RAG – question reçue : {Question}", request.Question);

        // 1. Embedding de la question
        var embedding = await _embeddingService.EmbedAsync(request.Question, cancellationToken);

        // 2. Recherche vectorielle (cosine distance <=>)
        var matches = await SearchSimilarIncidentsAsync(embedding, cancellationToken);
        _logger.LogInformation("RAG – {Count} incidents similaires trouvés.", matches.Count);

        // 3. Construction du prompt
        var prompt = BuildPrompt(request.Question, matches);
        _logger.LogDebug("RAG – prompt construit :\n{Prompt}", prompt);

        // 4. Génération via Ollama
        var answer = await GenerateAnswerAsync(prompt, cancellationToken);

        return new AskResponse(answer, matches.Select(m => m.Id).ToList().AsReadOnly());
    }

    // ── Recherche vectorielle ────────────────────────────────────
    private async Task<IReadOnlyList<IncidentMatch>> SearchSimilarIncidentsAsync(
        float[] embedding,
        CancellationToken cancellationToken)
    {
        var vectorLiteral = "[" + string.Join(",", embedding) + "]";

        const string sql = """
            SELECT i.id          AS Id,
                   i.search_text AS SearchText,
                   (ie.embedding <=> @Embedding::vector) AS Distance
            FROM incident_embeddings ie
            JOIN incidents i ON i.id = ie.incident_id
            ORDER BY ie.embedding <=> @Embedding::vector
            LIMIT 5;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        var results = await connection.QueryAsync<IncidentMatch>(
            new CommandDefinition(sql, new { Embedding = vectorLiteral }, cancellationToken: cancellationToken));

        return results.AsList().AsReadOnly();
    }

    // ── Construction du prompt ───────────────────────────────────
    private static string BuildPrompt(string question, IReadOnlyList<IncidentMatch> matches)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Contexte :");
        for (int i = 0; i < matches.Count; i++)
            sb.AppendLine($"Incident {i + 1}: {matches[i].SearchText}");

        sb.AppendLine();
        sb.AppendLine("Question :");
        sb.AppendLine(question);
        sb.AppendLine();
        sb.AppendLine("Réponds de manière claire et concise en te basant uniquement sur ces incidents.");

        return sb.ToString();
    }

    // ── Appel LLM Ollama /api/generate ──────────────────────────
    private async Task<string> GenerateAnswerAsync(string prompt, CancellationToken cancellationToken)
    {
        var payload = new { model = _generationModel, prompt, stream = false };

        _logger.LogInformation("RAG – appel Ollama generate (modèle: {Model}).", _generationModel);

        using var response = await _httpClient.PostAsJsonAsync("/api/generate", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
                         cancellationToken: cancellationToken)
                     ?? throw new InvalidOperationException("Ollama /api/generate a retourné une réponse nulle.");

        return result.Response;
    }

    // ── Types internes ───────────────────────────────────────────
    private sealed record IncidentMatch(Guid Id, string SearchText, double Distance);

    private sealed record OllamaGenerateResponse(
        [property: JsonPropertyName("response")] string Response
    );
}
