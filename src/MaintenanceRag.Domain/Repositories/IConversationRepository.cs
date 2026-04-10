namespace MaintenanceRag.Domain.Repositories;

using MaintenanceRag.Domain.Entities;

public interface IConversationRepository
{
    Task<Guid> SaveAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task<IEnumerable<Conversation>> GetRecentAsync(int limit = 10, CancellationToken cancellationToken = default);
}
