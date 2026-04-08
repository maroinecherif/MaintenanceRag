using MaintenanceRag.Domain.Entities;

namespace MaintenanceRag.Domain.Repositories;

public interface IIncidentRepository
{
    Task<IReadOnlyList<Incident>> GetAllAsync();
    Task<Incident?> GetByIdAsync(Guid id);
}
