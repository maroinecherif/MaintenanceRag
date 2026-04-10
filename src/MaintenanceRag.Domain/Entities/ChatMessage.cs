namespace MaintenanceRag.Domain.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid[] Sources { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
