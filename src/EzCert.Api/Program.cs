using EzCert.Api.Data;
using EzCert.Api.Endpoints;
using EzCert.Api.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=ezcert;Username=ezcert;Password=ezcert";

builder.Services.AddDbContext<EzCertDbContext>(opt => opt.UseNpgsql(connectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string CorsPolicy = "spa";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "https://d3ku4gdv1yd16a.cloudfront.net", "http://localhost:4173" };
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// RAG pipeline (Bedrock + Qdrant). AWS credentials come from the default chain (env vars).
builder.Services.Configure<RagOptions>(builder.Configuration.GetSection("Rag"));
builder.Services.AddSingleton(sp =>
{
    var o = sp.GetRequiredService<IOptions<RagOptions>>().Value;
    return new QdrantClient(o.QdrantHost, o.QdrantPort, o.QdrantHttps);
});
builder.Services.AddSingleton<IBedrockClient, BedrockClient>();
builder.Services.AddSingleton<IQdrantIndex, QdrantIndex>();
builder.Services.AddScoped<RagService>();

var app = builder.Build();

// Apply migrations + seed the AZ-900 bank. Non-fatal: the app still starts (and /health responds)
// even when the database is unavailable, so liveness checks stay meaningful.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<EzCertDbContext>();
        await db.Database.MigrateAsync();
        await JsonSeeder.SeedAsync(db, app.Environment.ContentRootPath, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Startup database migrate/seed failed. API is up but data endpoints will error until the DB is reachable.");
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(CorsPolicy);

app.MapHealthEndpoints();
app.MapCatalogEndpoints();
app.MapQuestionEndpoints();
app.MapSessionEndpoints();
app.MapRagEndpoints();

app.Run();
