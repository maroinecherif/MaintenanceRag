using Dapper;
using MaintenanceRag.Application.Services;
using MaintenanceRag.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MaintenanceRag.Infrastructure.Services;

public sealed class IncidentIndexingService
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly string _connectionString;
    private readonly ILogger<IncidentIndexingService> _logger;

    public IncidentIndexingService(
        IIncidentRepository incidentRepository,
        IEmbeddingService embeddingService,
        string connectionString,
        ILogger<IncidentIndexingService> logger)
    {
        _incidentRepository = incidentRepository;
        _embeddingService = embeddingService;
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task ReindexAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Début de l'indexation des incidents.");

        var incidents = await _incidentRepository.GetAllAsync();
        _logger.LogInformation("{Count} incidents trouvés à indexer.", incidents.Count);

        const string sql = """
            INSERT INTO incident_embeddings (incident_id, embedding)
            VALUES (@IncidentId, @Embedding::vector)
            ON CONFLICT (incident_id)
            DO UPDATE SET embedding = @Embedding::vector;
            """;

        int indexed = 0;
        foreach (var incident in incidents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var embedding = await _embeddingService.EmbedAsync(incident.SearchText, cancellationToken);
            var vectorLiteral = "[" + string.Join(",", embedding) + "]";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.ExecuteAsync(sql, new { IncidentId = incident.Id, Embedding = vectorLiteral });

            indexed++;
            Console.WriteLine($"  [{indexed}/{incidents.Count}] ✓ {incident.EquipmentName} ({incident.Id})");
        }

        _logger.LogInformation("Indexation terminée — {Count} incidents indexés.", indexed);
    }
}
