using EzCert.Processor.Features.Attempts;
using EzCert.Processor.Features.Generation;
using EzCert.Processor.Features.Guests;
using EzCert.Processor.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=ezcert;Username=ezcert;Password=ezcert";

builder.Services.AddDbContext<EzCertDbContext>(opt => opt.UseNpgsql(connectionString));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string CorsPolicy = "spa";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:4173" };
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var qdrantHost = builder.Configuration["Qdrant:Host"] ?? "localhost";
var qdrantPort = int.TryParse(builder.Configuration["Qdrant:Port"], out var qp) ? qp : 6333;
var qdrantHttps = bool.TryParse(builder.Configuration["Qdrant:Https"], out var qh) && qh;
builder.Services.AddSingleton(new QdrantClient(qdrantHost, qdrantPort, qdrantHttps));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EzCertDbContext>();
    try { await db.Database.MigrateAsync(); }
    catch (Exception ex)
    {
        var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        log.LogError(ex, "Startup migrate failed; API up but data endpoints need the DB");
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(CorsPolicy);
app.UseGuestIdentity();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "ezcert-processor" }));

app.MapGenerationEndpoints();
app.MapAttemptEndpoints();

app.Run();
