using System.Text;
using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.AspNetCore.Mvc;

// Bedrock gateway (AD-14): server-to-server only, bearer-secret protected.
// Runs in the hackathon sandbox with an IAM instance role (bedrock:InvokeModel).
// The processor calls these three endpoints instead of Bedrock directly.

var builder = WebApplication.CreateBuilder(args);

const string Secret = "GATEWAY_SECRET";
const string EmbedModel = "amazon.titan-embed-text-v2:0";
const string GenModel = "amazon.nova-micro-v1:0";

var secret = builder.Configuration[Secret] ?? throw new InvalidOperationException($"{Secret} env var is required");
var region = builder.Configuration["AWS_REGION"] ?? "us-east-1";
var bedrock = new AmazonBedrockRuntimeClient(Amazon.RegionEndpoint.GetBySystemName(region));

var app = builder.Build();

// Public liveness endpoint (App Runner health check cannot send auth headers).
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "ezcert-bedrock-gateway" }));

app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path == "/health")
    {
        await next();
        return;
    }
    var auth = ctx.Request.Headers.Authorization.ToString();
    if (!string.Equals(auth, $"Bearer {secret}", StringComparison.Ordinal))
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsync("unauthorized");
        return;
    }
    await next();
});

// POST /api/bedrock/embed  { text } -> { embedding: number[] }
app.MapPost("/api/bedrock/embed", async ([FromBody] EmbedRequest req, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest(new { error = "text is required" });

    var resp = await bedrock.InvokeModelAsync(new InvokeModelRequest
    {
        ModelId = EmbedModel,
        Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { inputText = req.Text }))),
    }, ct);

    using var doc = JsonDocument.Parse(resp.Body);
    var embedding = doc.RootElement.GetProperty("embedding");
    var values = new List<float>();
    foreach (var e in embedding.EnumerateArray())
        values.Add(e.GetSingle());
    return Results.Ok(new { embedding = values });
});

// POST /api/bedrock/generate  { prompt, system?, maxTokens?, temperature? } -> { text }
app.MapPost("/api/bedrock/generate", async ([FromBody] GenerateRequest req, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Prompt))
        return Results.BadRequest(new { error = "prompt is required" });

    var system = new List<SystemContentBlock>();
    if (!string.IsNullOrWhiteSpace(req.System))
        system.Add(new SystemContentBlock { Text = req.System });

    var resp = await bedrock.ConverseAsync(new ConverseRequest
    {
        ModelId = GenModel,
        Messages = new List<Message>
        {
            new Message { Role = ConversationRole.User, Content = new List<ContentBlock> { new ContentBlock { Text = req.Prompt } } },
        },
        System = system.Count > 0 ? system : null,
        InferenceConfig = new InferenceConfiguration
        {
            MaxTokens = req.MaxTokens ?? 700,
            Temperature = req.Temperature ?? 0.3f,
        },
    }, ct);

    var text = string.Concat(resp.Output?.Message?.Content?.Select(c => c.Text ?? "") ?? Enumerable.Empty<string>());
    return Results.Ok(new { text });
});

// POST /api/bedrock/explain  { question, correctAnswer, userAnswer?, slug? } -> { text }
app.MapPost("/api/bedrock/explain", async ([FromBody] ExplainRequest req, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Question) || string.IsNullOrWhiteSpace(req.CorrectAnswer))
        return Results.BadRequest(new { error = "question and correctAnswer are required" });

    var system = "You are a friendly cloud certification tutor. Explain concisely (2-4 sentences) and be encouraging and factual.";
    var wrong = string.IsNullOrWhiteSpace(req.UserAnswer)
        ? ""
        : $"\nThe learner answered: {req.UserAnswer}. If that is incorrect, gently explain why.";
    var prompt = $"Question: {req.Question}\nCorrect answer: {req.CorrectAnswer}{wrong}\n\nExplain why the correct answer is right.";

    var resp = await bedrock.ConverseAsync(new ConverseRequest
    {
        ModelId = GenModel,
        Messages = new List<Message>
        {
            new Message { Role = ConversationRole.User, Content = new List<ContentBlock> { new ContentBlock { Text = prompt } } },
        },
        System = new List<SystemContentBlock> { new SystemContentBlock { Text = system } },
        InferenceConfig = new InferenceConfiguration { MaxTokens = 400, Temperature = 0.3f },
    }, ct);

    var text = string.Concat(resp.Output?.Message?.Content?.Select(c => c.Text ?? "") ?? Enumerable.Empty<string>());
    return Results.Ok(new { text });
});

app.Run();

public record EmbedRequest(string Text);
public record GenerateRequest(string Prompt, string? System, int? MaxTokens, float? Temperature);
public record ExplainRequest(string Question, string CorrectAnswer, string? UserAnswer, string? Slug);
