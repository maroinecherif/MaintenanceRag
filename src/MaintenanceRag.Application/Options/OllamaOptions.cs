namespace MaintenanceRag.Application.Options;

public sealed class OllamaOptions
{
    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string EmbeddingModel { get; init; } = "all-minilm";
    public string GenerationModel { get; init; } = "mistral";
}
