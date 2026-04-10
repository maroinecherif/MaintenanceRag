namespace MaintenanceRag.Domain.Repositories;

using MaintenanceRag.Domain.Entities;

public interface IChatRepository
{
    Task<Guid> CreateSessionAsync(ChatSession session, CancellationToken ct = default);
    Task<IEnumerable<ChatSession>> GetRecentSessionsAsync(int limit = 20, CancellationToken ct = default);
    Task<ChatSession?> GetSessionWithMessagesAsync(Guid sessionId, CancellationToken ct = default);
    Task AddMessageAsync(ChatMessage message, CancellationToken ct = default);
    Task UpdateSessionTitleAsync(Guid sessionId, string title, CancellationToken ct = default);
}
