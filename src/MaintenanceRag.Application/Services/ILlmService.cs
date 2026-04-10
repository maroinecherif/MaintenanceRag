namespace MaintenanceRag.Application.Services;

public interface ILlmService
{
    Task<string> GenerateAnswerAsync(string prompt, CancellationToken cancellationToken = default);
}
