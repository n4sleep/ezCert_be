using EzCert.Processor.Features.Attempts;
using EzCert.Processor.Features.Exams;
using EzCert.Processor.Features.Generation;
using EzCert.Processor.Features.Guests;
using EzCert.Processor.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=ezcert;Username=ezcert;Password=ezcert";

// Database provider: "sqlite" (hosted/App Runner, ephemeral) or "postgres" (local dev).
var dbProvider = builder.Configuration["Database:Provider"] ?? "postgres";

builder.Services.AddDbContext<EzCertDbContext>(opt =>
{
    if (dbProvider == "sqlite")
    {
        var sqlitePath = builder.Configuration["Database:SqlitePath"] ?? "/data/ezcert.db";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sqlitePath))!);
        opt.UseSqlite($"Data Source={sqlitePath}");
    }
    else
    {
        opt.UseNpgsql(connectionString);
    }
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string CorsPolicy = "spa";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:4173" };
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var qdrantHost = builder.Configuration["Qdrant:Host"] ?? "localhost";
var qdrantPort = int.TryParse(builder.Configuration["Qdrant:Port"], out var qp) ? qp : 6333;
var qdrantHttps = bool.TryParse(builder.Configuration["Qdrant:Https"], out var qh) && qh;
builder.Services.AddSingleton(new QdrantClient(qdrantHost, qdrantPort, qdrantHttps));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EzCertDbContext>();
    try
    {
        if (dbProvider == "sqlite")
            await db.Database.EnsureCreatedAsync(); // SQLite: schema from the model (no migration set)
        else
            await db.Database.MigrateAsync();       // Postgres: EF migrations
    }
    catch (Exception ex)
    {
        var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        log.LogError(ex, "Startup database init failed; API up but data endpoints need the DB");
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(CorsPolicy);
app.UseGuestIdentity();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "ezcert-processor" }));

app.MapGenerationEndpoints();
app.MapAttemptEndpoints();
app.MapExamShareEndpoints();

app.Run();
