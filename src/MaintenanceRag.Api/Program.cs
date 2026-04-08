using MaintenanceRag.Application.DTOs;
using MaintenanceRag.Application.Options;
using MaintenanceRag.Application.Services;
using MaintenanceRag.Domain.Repositories;
using MaintenanceRag.Infrastructure.Embeddings;
using MaintenanceRag.Infrastructure.Repositories;
using MaintenanceRag.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Swagger ──────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MaintenanceRag API", Version = "v1" });
});

// ── Repository ───────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");

builder.Services.AddSingleton<IIncidentRepository>(new IncidentRepository(connectionString));

// ── Ollama Embedding ─────────────────────────────────────────
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));

var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";

builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(client =>
{
    client.BaseAddress = new Uri(ollamaBaseUrl);
});

// ── Ollama Generation (timeout 2 min pour LLM) ────────────────
builder.Services.AddHttpClient("ollama-generate", client =>
{
    client.BaseAddress = new Uri(ollamaBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(2);
});

// ── Indexing Service ─────────────────────────────────────────
builder.Services.AddScoped<IncidentIndexingService>(sp => new IncidentIndexingService(
    sp.GetRequiredService<IIncidentRepository>(),
    sp.GetRequiredService<IEmbeddingService>(),
    connectionString,
    sp.GetRequiredService<ILogger<IncidentIndexingService>>()
));

// ── RAG Service ───────────────────────────────────────────────
var generationModel = builder.Configuration["Ollama:GenerationModel"] ?? "mistral";

builder.Services.AddScoped<RagService>(sp => new RagService(
    sp.GetRequiredService<IEmbeddingService>(),
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama-generate"),
    connectionString,
    generationModel,
    sp.GetRequiredService<ILogger<RagService>>()
));

var app = builder.Build();

// ── Swagger UI ───────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MaintenanceRag API v1"));

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// ── Endpoints ────────────────────────────────────────────────
app.MapGet("/incidents", async (IIncidentRepository repo) =>
{
    var incidents = await repo.GetAllAsync();
    return Results.Ok(incidents);
})
.WithName("GetAllIncidents")
.WithTags("Incidents")
.WithSummary("Retourne tous les incidents triés par date décroissante.");

app.MapGet("/incidents/{id:guid}", async (Guid id, IIncidentRepository repo) =>
{
    var incident = await repo.GetByIdAsync(id);
    return incident is null
        ? Results.NotFound(new { message = $"Incident {id} introuvable." })
        : Results.Ok(incident);
})
.WithName("GetIncidentById")
.WithTags("Incidents")
.WithSummary("Retourne un incident par son identifiant UUID.");

app.MapPost("/incidents/reindex", async (IncidentIndexingService indexingService, CancellationToken ct) =>
{
    await indexingService.ReindexAllAsync(ct);
    return Results.Ok(new { status = "reindexed" });
})
.WithName("ReindexIncidents")
.WithTags("Incidents")
.WithSummary("Génère et sauvegarde les embeddings Ollama pour tous les incidents.");

// ── RAG ───────────────────────────────────────────────────────
app.MapPost("/ask", async (AskRequest request, RagService ragService, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
        return Results.BadRequest(new { error = "La question ne peut pas être vide." });

    var response = await ragService.AskAsync(request, ct);
    return Results.Ok(response);
})
.WithName("Ask")
.WithTags("RAG")
.WithSummary("Pose une question — recherche vectorielle + génération LLM via Ollama.");

app.Run();
