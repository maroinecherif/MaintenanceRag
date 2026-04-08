namespace MaintenanceRag.Domain.Entities;

public sealed class Incident
{
    public Guid Id { get; init; }
    public string EquipmentName { get; init; } = string.Empty;
    public DateTime IncidentDate { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? Cause { get; init; }
    public string? Solution { get; init; }
    public string SearchText { get; init; } = string.Empty;
}
