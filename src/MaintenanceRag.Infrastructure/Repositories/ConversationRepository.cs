namespace MaintenanceRag.Infrastructure.Repositories;

using Dapper;
using MaintenanceRag.Domain.Entities;
using MaintenanceRag.Domain.Repositories;
using Npgsql;

public class ConversationRepository : IConversationRepository
{
    private readonly string _connectionString;

    public ConversationRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid> SaveAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO conversations (id, question, answer, equipment, sources, created_at)
            VALUES (@Id, @Question, @Answer, @Equipment, @Sources, @CreatedAt)
            RETURNING id;
        ";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var id = await connection.QuerySingleAsync<Guid>(
            new CommandDefinition(sql, conversation, cancellationToken: cancellationToken)
        );

        return id;
    }

    public async Task<IEnumerable<Conversation>> GetRecentAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, question, answer, equipment, sources, created_at
            FROM conversations
            ORDER BY created_at DESC
            LIMIT @Limit;
        ";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var conversations = await connection.QueryAsync<Conversation>(
            new CommandDefinition(sql, new { Limit = limit }, cancellationToken: cancellationToken)
        );

        return conversations;
    }
}
