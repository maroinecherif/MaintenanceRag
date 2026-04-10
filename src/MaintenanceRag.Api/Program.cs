using MaintenanceRag.Application.DTOs;
using MaintenanceRag.Application.Options;
using MaintenanceRag.Application.Services;
using MaintenanceRag.Domain.Entities;
using MaintenanceRag.Domain.Repositories;
using MaintenanceRag.Infrastructure.Embeddings;
using MaintenanceRag.Infrastructure.Llm;
using MaintenanceRag.Infrastructure.Repositories;
using MaintenanceRag.Infrastructure.Services;

// Snake_case → PascalCase mapping pour Dapper (ex: created_at → CreatedAt)
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

// ── Swagger ──────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MaintenanceRag API", Version = "v1" });
});

// ── Razor Pages (UI de démo) ──────────────────────────────────
builder.Services.AddRazorPages();

// ── Repository ───────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");

builder.Services.AddSingleton<IIncidentRepository>(new IncidentRepository(connectionString));
builder.Services.AddSingleton<IConversationRepository>(new ConversationRepository(connectionString));
builder.Services.AddSingleton<IChatRepository>(new ChatRepository(connectionString));

// ── Ollama Embedding ─────────────────────────────────────────
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));

var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";

builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(client =>
{
    client.BaseAddress = new Uri(ollamaBaseUrl);
});

// ── Ollama LLM (timeout 2 min pour la génération) ─────────────
builder.Services.AddHttpClient<ILlmService, OllamaLlmService>(client =>
{
    client.BaseAddress = new Uri(ollamaBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(2);
});

// ── Prompt Builder (stateless) ────────────────────────────────
builder.Services.AddSingleton<PromptBuilder>();

// ── Indexing Service ─────────────────────────────────────────
builder.Services.AddScoped<IncidentIndexingService>(sp => new IncidentIndexingService(
    sp.GetRequiredService<IIncidentRepository>(),
    sp.GetRequiredService<IEmbeddingService>(),
    connectionString,
    sp.GetRequiredService<ILogger<IncidentIndexingService>>()
));

// ── RAG Service ───────────────────────────────────────────────
builder.Services.AddScoped<RagService>(sp => new RagService(
    sp.GetRequiredService<IEmbeddingService>(),
    sp.GetRequiredService<ILlmService>(),
    sp.GetRequiredService<PromptBuilder>(),
    connectionString,
    sp.GetRequiredService<ILogger<RagService>>()
));

var app = builder.Build();

// ── Swagger UI ───────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MaintenanceRag API v1"));

app.UseStaticFiles();
app.MapRazorPages();

app.MapGet("/", () => Results.Redirect("/ask-ui")).ExcludeFromDescription();

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

// ── Conversations (Historique) ────────────────────────────────
app.MapPost("/conversations", async (SaveConversationRequest request, IConversationRepository repo, CancellationToken ct) =>
{
    var conversation = new Conversation
    {
        Id = Guid.NewGuid(),
        Question = request.Question,
        Answer = request.Answer,
        Equipment = request.Equipment,
        Sources = request.Sources ?? [],
        CreatedAt = DateTime.UtcNow
    };

    var id = await repo.SaveAsync(conversation, ct);
    return Results.Created($"/conversations/{id}", new { id });
})
.WithName("SaveConversation")
.WithTags("Conversations")
.WithSummary("Enregistre une question/réponse dans l'historique.");

app.MapGet("/conversations", async (IConversationRepository repo, CancellationToken ct) =>
{
    var conversations = await repo.GetRecentAsync(10, ct);

    var result = conversations.Select(c => new ConversationDto(
        c.Id,
        c.Question,
        c.Answer,
        c.Equipment,
        c.Sources,
        c.CreatedAt
    )).ToList();

    return Results.Ok(result);
})
.WithName("GetConversationHistory")
.WithTags("Conversations")
.WithSummary("Retourne les 10 dernières conversations.");

// ── Chat (Architecture multi-tours) ──────────────────────────
app.MapPost("/chat/sessions", async (CreateSessionRequest req, IChatRepository chatRepo, CancellationToken ct) =>
{
    var session = new ChatSession
    {
        Id = Guid.NewGuid(),
        Title = "Nouvelle conversation",
        Equipment = req.Equipment,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    var id = await chatRepo.CreateSessionAsync(session, ct);
    return Results.Created($"/chat/sessions/{id}", new { id, title = session.Title });
})
.WithName("CreateChatSession")
.WithTags("Chat")
.WithSummary("Crée une nouvelle session de conversation.");

app.MapGet("/chat/sessions", async (IChatRepository chatRepo, CancellationToken ct) =>
{
    var sessions = await chatRepo.GetRecentSessionsAsync(20, ct);
    var result = sessions.Select(s => new ChatSessionSummaryDto(s.Id, s.Title, s.Equipment, s.UpdatedAt));
    return Results.Ok(result);
})
.WithName("GetChatSessions")
.WithTags("Chat")
.WithSummary("Retourne les 20 dernières sessions de conversation.");

app.MapGet("/chat/sessions/{id:guid}", async (Guid id, IChatRepository chatRepo, CancellationToken ct) =>
{
    var session = await chatRepo.GetSessionWithMessagesAsync(id, ct);
    if (session is null) return Results.NotFound(new { error = "Session introuvable." });

    var result = new ChatSessionDetailDto(
        session.Id, session.Title, session.Equipment, session.CreatedAt,
        session.Messages.Select(m => new ChatMessageDto(m.Id, m.Role, m.Content, m.Sources, m.CreatedAt)).ToList()
    );
    return Results.Ok(result);
})
.WithName("GetChatSession")
.WithTags("Chat")
.WithSummary("Retourne une session avec tous ses messages.");

app.MapPost("/chat/sessions/{id:guid}/ask", async (Guid id, ChatAskRequest req, IChatRepository chatRepo, RagService ragService, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Question))
        return Results.BadRequest(new { error = "La question ne peut pas être vide." });

    var session = await chatRepo.GetSessionWithMessagesAsync(id, ct);
    if (session is null) return Results.NotFound(new { error = "Session introuvable." });

    bool isFirstMessage = session.Messages.Count == 0;

    var userMessage = new ChatMessage
    {
        Id = Guid.NewGuid(), SessionId = id, Role = "user",
        Content = req.Question, Sources = [], CreatedAt = DateTime.UtcNow
    };
    await chatRepo.AddMessageAsync(userMessage, ct);

    var equipment = req.Equipment ?? session.Equipment;
    var response = await ragService.AskAsync(new AskRequest(req.Question, equipment), ct);

    var assistantMessage = new ChatMessage
    {
        Id = Guid.NewGuid(), SessionId = id, Role = "assistant",
        Content = response.Answer, Sources = [.. response.Sources], CreatedAt = DateTime.UtcNow
    };
    await chatRepo.AddMessageAsync(assistantMessage, ct);

    if (isFirstMessage)
    {
        var title = req.Question.Length > 50 ? req.Question[..50] + "…" : req.Question;
        await chatRepo.UpdateSessionTitleAsync(id, title, ct);
    }

    return Results.Ok(new ChatAskResponse(assistantMessage.Id, response.Answer, [.. response.Sources]));
})
.WithName("ChatAsk")
.WithTags("Chat")
.WithSummary("Pose une question dans une session — RAG + LLM.");

app.Run();
