namespace MaintenanceRag.Infrastructure.Repositories;

using Dapper;
using MaintenanceRag.Domain.Entities;
using MaintenanceRag.Domain.Repositories;
using Npgsql;

public class ChatRepository : IChatRepository
{
    private readonly string _connectionString;

    public ChatRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid> CreateSessionAsync(ChatSession session, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO chat_sessions (id, title, equipment, created_at, updated_at)
            VALUES (@Id, @Title, @Equipment, @CreatedAt, @UpdatedAt)
            RETURNING id;
        ";
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return await conn.QuerySingleAsync<Guid>(new CommandDefinition(sql, session, cancellationToken: ct));
    }

    public async Task<IEnumerable<ChatSession>> GetRecentSessionsAsync(int limit = 20, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, title, equipment, created_at, updated_at
            FROM chat_sessions
            ORDER BY updated_at DESC
            LIMIT @Limit;
        ";
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return await conn.QueryAsync<ChatSession>(new CommandDefinition(sql, new { Limit = limit }, cancellationToken: ct));
    }

    public async Task<ChatSession?> GetSessionWithMessagesAsync(Guid sessionId, CancellationToken ct = default)
    {
        const string sessionSql = @"
            SELECT id, title, equipment, created_at, updated_at
            FROM chat_sessions WHERE id = @SessionId;
        ";
        const string messagesSql = @"
            SELECT id, session_id, role, content, sources, created_at
            FROM chat_messages WHERE session_id = @SessionId ORDER BY created_at ASC;
        ";
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var session = await conn.QuerySingleOrDefaultAsync<ChatSession>(
            new CommandDefinition(sessionSql, new { SessionId = sessionId }, cancellationToken: ct));

        if (session is null) return null;

        var messages = await conn.QueryAsync<ChatMessage>(
            new CommandDefinition(messagesSql, new { SessionId = sessionId }, cancellationToken: ct));

        session.Messages = messages.ToList();
        return session;
    }

    public async Task AddMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        const string insertSql = @"
            INSERT INTO chat_messages (id, session_id, role, content, sources, created_at)
            VALUES (@Id, @SessionId, @Role, @Content, @Sources, @CreatedAt);
        ";
        const string updateSql = @"
            UPDATE chat_sessions SET updated_at = @CreatedAt WHERE id = @SessionId;
        ";
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(insertSql, message, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition(updateSql, new { message.CreatedAt, message.SessionId }, cancellationToken: ct));
    }

    public async Task UpdateSessionTitleAsync(Guid sessionId, string title, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE chat_sessions SET title = @Title, updated_at = CURRENT_TIMESTAMP WHERE id = @Id;
        ";
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = sessionId, Title = title }, cancellationToken: ct));
    }
}
