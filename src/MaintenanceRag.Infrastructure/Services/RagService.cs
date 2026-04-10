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
    private readonly ILlmService _llmService;
    private readonly PromptBuilder _promptBuilder;
    private readonly string _connectionString;
    private readonly ILogger<RagService> _logger;

    public RagService(
        IEmbeddingService embeddingService,
        ILlmService llmService,
        PromptBuilder promptBuilder,
        string connectionString,
        ILogger<RagService> logger)
    {
        _embeddingService = embeddingService;
        _llmService = llmService;
        _promptBuilder = promptBuilder;
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<AskResponse> AskAsync(AskRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("RAG – question : {Question}", request.Question);

        try
        {
            // 1. Embedding de la question
            var embedding = await _embeddingService.EmbedAsync(request.Question, cancellationToken);

            // 2. Hybrid search (vectoriel <=> + full-text ts_rank)
            var rawMatches = await HybridSearchAsync(embedding, request.Question, request.Equipment, cancellationToken);

            if (rawMatches.Count == 0)
            {
                _logger.LogWarning("RAG – aucun incident trouvé pour : {Question}", request.Question);
                return new AskResponse("Aucun incident pertinent trouvé dans la base de données pour cette question.", []);
            }

            // 3. Reranking combiné (1/distance + textScore)
            var top = Rerank(rawMatches);
            _logger.LogInformation("RAG – top incidents retenus : {Ids}",
                string.Join(", ", top.Select(x => x.Id)));

            // 4. Construction du prompt
            var incidentDtos = top.Select(x => new IncidentMatchDto(x.Id, x.SearchText)).ToList();
            var prompt = _promptBuilder.Build(request.Question, incidentDtos);
            _logger.LogInformation("RAG – prompt construit ({Length} caractères).", prompt.Length);

            // 5. Génération LLM avec timeout 60s
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            var answer = await _llmService.GenerateAnswerAsync(prompt, cts.Token);

            return new AskResponse(answer, top.Select(x => x.Id).ToList().AsReadOnly());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("RAG – timeout (60s) atteint lors de la génération.");
            return new AskResponse("La génération a pris trop de temps. Veuillez réessayer ou simplifier la question.", []);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "RAG – erreur HTTP lors de l'appel Ollama.");
            return new AskResponse("Erreur de communication avec Ollama. Vérifiez qu'il est démarré.", []);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG – erreur inattendue.");
            throw;
        }
    }

    // ── Hybrid Search ────────────────────────────────────────────
    private async Task<IReadOnlyList<IncidentMatch>> HybridSearchAsync(
        float[] embedding,
        string query,
        string? equipment,
        CancellationToken cancellationToken)
    {
        var vectorLiteral = "[" + string.Join(",", embedding) + "]";

        var equipmentFilter = string.IsNullOrWhiteSpace(equipment)
            ? string.Empty
            : "AND i.equipment_name ILIKE '%' || @Equipment || '%'";

        var sql = $"""
            SELECT i.id          AS Id,
                   i.search_text AS SearchText,
                   (ie.embedding <=> @Embedding::vector)        AS Distance,
                   ts_rank(
                       to_tsvector('french', i.search_text),
                       plainto_tsquery('french', @Query)
                   )::double precision                          AS TextScore
            FROM incident_embeddings ie
            JOIN incidents i ON i.id = ie.incident_id
            WHERE 1=1 {equipmentFilter}
            ORDER BY ie.embedding <=> @Embedding::vector ASC
            LIMIT 10;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        var results = await connection.QueryAsync<IncidentMatch>(
            new CommandDefinition(
                sql,
                new { Embedding = vectorLiteral, Query = query, Equipment = equipment ?? string.Empty },
                cancellationToken: cancellationToken));

        return results.AsList().AsReadOnly();
    }

    // ── Reranking : score = (1 / (distance + ε)) + textScore ────
    private static IReadOnlyList<RankedIncident> Rerank(IReadOnlyList<IncidentMatch> matches) =>
        matches
            .Select(x => new RankedIncident(
                Id: x.Id,
                SearchText: x.SearchText,
                Score: (1.0 / (x.Distance + 0.0001)) + x.TextScore,
                Distance: x.Distance,
                TextScore: x.TextScore))
            .OrderByDescending(x => x.Score)
            .Take(5)
            .ToList()
            .AsReadOnly();

    // ── Types internes (mapping DB + reranking) ──────────────────
    private sealed record IncidentMatch(Guid Id, string SearchText, double Distance, double TextScore);

    private sealed record RankedIncident(Guid Id, string SearchText, double Score, double Distance, double TextScore);
}

