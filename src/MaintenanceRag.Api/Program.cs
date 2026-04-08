using MaintenanceRag.Domain.Repositories;
using MaintenanceRag.Infrastructure.Repositories;

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

app.Run();
