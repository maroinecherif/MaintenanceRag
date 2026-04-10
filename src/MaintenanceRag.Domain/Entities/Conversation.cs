namespace MaintenanceRag.Domain.Entities;

public class Conversation
{
    public Guid Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public string? Equipment { get; set; }
    public Guid[] Sources { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
