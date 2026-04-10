namespace MaintenanceRag.Application.DTOs;

public sealed record AskRequest(string Question, string? Equipment = null);
