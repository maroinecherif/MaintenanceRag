namespace MaintenanceRag.Application.DTOs;

public record SaveConversationRequest(
    string Question,
    string? Answer = null,
    string? Equipment = null,
    Guid[]? Sources = null
);

public record ConversationDto(
    Guid Id,
    string Question,
    string? Answer,
    string? Equipment,
    Guid[] Sources,
    DateTime CreatedAt
);
