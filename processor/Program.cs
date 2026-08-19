using EzCert.Processor.Features.Attempts;
using EzCert.Processor.Features.Diagnostics;
using EzCert.Processor.Features.Exams;
using EzCert.Processor.Features.Generation;
using EzCert.Processor.Features.Guests;
using EzCert.Processor.Features.Sources;
using EzCert.Processor.Infrastructure.Bedrock;
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
var qdrantPort = int.TryParse(builder.Configuration["Qdrant:Port"], out var qp) ? qp : 6334;
var qdrantHttps = bool.TryParse(builder.Configuration["Qdrant:Https"], out var qh) && qh;
var qdrantKey = builder.Configuration["Qdrant:ApiKey"] ?? "";
builder.Services.AddSingleton(new QdrantClient(qdrantHost, qdrantPort, qdrantHttps, qdrantKey));
builder.Services.AddScoped<SeedService>();
builder.Services.AddScoped<SourceService>();
builder.Services.AddScoped<GenerationService>();
builder.Services.AddScoped<DiscoveryService>();
builder.Services.AddHttpClient<EzCert.Processor.Infrastructure.CrawlerClient.CrawlerClient>();
builder.Services.AddSingleton<JobQueue>();
builder.Services.AddHostedService<ProcessingWorker>();

// Bedrock access (AD-14): "gateway" = rich-sandbox HTTP gateway (hosted),
// "direct" = local Bedrock via SSO credentials (dev).
var bedrockMode = builder.Configuration["Bedrock:Mode"] ?? "direct";
var embedModel = builder.Configuration["Bedrock:EmbedModel"] ?? "amazon.titan-embed-text-v2:0";
var genModel = builder.Configuration["Bedrock:GenModel"] ?? "amazon.nova-micro-v1:0";
if (bedrockMode == "gateway")
{
    var gwUrl = builder.Configuration["BedrockGateway:Url"]
        ?? throw new InvalidOperationException("BedrockGateway:Url is required when Bedrock:Mode=gateway");
    var gwSecret = builder.Configuration["BedrockGateway:Secret"]
        ?? throw new InvalidOperationException("BedrockGateway:Secret is required when Bedrock:Mode=gateway");
    builder.Services.AddSingleton<IBedrockClient>(new GatewayBedrockClient(gwUrl, gwSecret));
}
else
{
    var region = builder.Configuration["AWS_REGION"] ?? "us-east-1";
    builder.Services.AddSingleton<IBedrockClient>(new DirectBedrockClient(embedModel, genModel, region));
}

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors(CorsPolicy);
app.UseGuestIdentity();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "ezcert-processor" }));

// Same-origin health path through the CloudFront /api/* behavior (DB-free so it
// stays meaningful when Postgres is down — unlike /health behind the middleware).
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "ezcert-processor" }));

app.MapGenerationEndpoints();
app.MapAttemptEndpoints();
app.MapExamShareEndpoints();
app.MapSourceEndpoints();

// POST /api/admin/seed { cert } -> re-seeds official content into Qdrant (AD-6).
// Demo/dev only: no auth, but harmless (official content is public docs).
app.MapPost("/api/admin/seed", async (HttpContext ctx, SeedService seed, CancellationToken ct) =>
{
    var cert = ctx.Request.Query["cert"].ToString() ?? "az900";
    var seedDir = Path.Combine(AppContext.BaseDirectory, "seed", "official");
    var n = await seed.SeedOfficialAsync(cert, seedDir, ct);
    return Results.Ok(new { cert, chunks = n, namespace_ = $"official:{cert.Trim().ToUpperInvariant().Replace("-", "")}" });
});

// Seed official content on startup: every cert in seed/official whose
// namespace is missing (per-cert idempotent; stable point IDs make re-seeds
// safe). Wrapped so Qdrant downtime never blocks the API from starting.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var seed = scope.ServiceProvider.GetRequiredService<SeedService>();
        var qd = scope.ServiceProvider.GetRequiredService<QdrantClient>();
        var seedDir = Path.Combine(AppContext.BaseDirectory, "seed", "official");
        var collections = await qd.ListCollectionsAsync();
        if (!collections.Contains(SeedService.Collection))
        {
            await qd.CreateCollectionAsync(SeedService.Collection,
                new Qdrant.Client.Grpc.VectorParams
                {
                    Size = SeedService.Dims,
                    Distance = Qdrant.Client.Grpc.Distance.Cosine,
                });
        }
        foreach (var dir in Directory.GetDirectories(seedDir))
        {
            var cert = Path.GetFileName(dir);
            var ns = $"official:{cert.Trim().ToUpperInvariant().Replace("-", "")}";
            if (await seed.NamespaceExistsAsync(ns))
            {
                var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                log.LogInformation("Startup seed skipped for {Cert}: namespace {Ns} already present", cert, ns);
                continue;
            }
            var n = await seed.SeedOfficialAsync(cert, seedDir);
            var log2 = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            log2.LogInformation("Startup seed complete for {Cert}: {Count} chunks", cert, n);
        }
    }
    catch (Exception ex)
    {
        var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        log.LogWarning(ex, "Startup Qdrant seed skipped (Qdrant unreachable?)");
    }
}

// Diagnostics endpoints only when explicitly enabled (Diagnostics:Enabled=true).
if (bool.TryParse(builder.Configuration["Diagnostics:Enabled"], out var diagEnabled) && diagEnabled)
    app.MapDiagnosticsEndpoints();

app.Run();
