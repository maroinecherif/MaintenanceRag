using Dapper;
using MaintenanceRag.Domain.Entities;
using MaintenanceRag.Domain.Repositories;
using Npgsql;

namespace MaintenanceRag.Infrastructure.Repositories;

public sealed class IncidentRepository : IIncidentRepository
{
    private readonly string _connectionString;

    public IncidentRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<Incident>> GetAllAsync()
    {
        const string sql = """
            SELECT id,
                   equipment_name  AS EquipmentName,
                   incident_date   AS IncidentDate,
                   description,
                   cause,
                   solution,
                   search_text     AS SearchText
            FROM incidents
            ORDER BY incident_date DESC
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        var result = await connection.QueryAsync<Incident>(sql);
        return result.AsList().AsReadOnly();
    }

    public async Task<Incident?> GetByIdAsync(Guid id)
    {
        const string sql = """
            SELECT id,
                   equipment_name  AS EquipmentName,
                   incident_date   AS IncidentDate,
                   description,
                   cause,
                   solution,
                   search_text     AS SearchText
            FROM incidents
            WHERE id = @Id
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<Incident>(sql, new { Id = id });
    }
}
