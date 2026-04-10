namespace MaintenanceRag.Application.DTOs;

public record CreateSessionRequest(string? Equipment = null);

public record ChatSessionSummaryDto(Guid Id, string Title, string? Equipment, DateTime UpdatedAt);

public record ChatMessageDto(Guid Id, string Role, string Content, Guid[] Sources, DateTime CreatedAt);

public record ChatSessionDetailDto(Guid Id, string Title, string? Equipment, DateTime CreatedAt, List<ChatMessageDto> Messages);

public record ChatAskRequest(string Question, string? Equipment = null);

public record ChatAskResponse(Guid MessageId, string Answer, Guid[] Sources);
