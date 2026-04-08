namespace MaintenanceRag.Application.DTOs;

public sealed record AskResponse(string Answer, IReadOnlyList<Guid> Sources);
