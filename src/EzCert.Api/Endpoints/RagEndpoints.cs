using EzCert.Api.Rag;
using Microsoft.Extensions.Options;

namespace EzCert.Api.Endpoints;

// RAG pipeline: indexing, semantic search, question generation, and explanations
// (Stories 1.5, 1.6, 2.3, 2.4, 4.5). All AI calls degrade gracefully to 503 when
// Bedrock credentials or Qdrant are unavailable, so the core app keeps working.
public static class RagEndpoints
{
    public sealed record GenerateRequest(string Topic, string? Slug);
    public sealed record ExplainRequest(string Question, string CorrectAnswer, string? UserAnswer, string? Slug);

    public static IEndpointRouteBuilder MapRagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rag").WithTags("Rag");

        group.MapGet("/status", async (IQdrantIndex index, IOptions<RagOptions> opt, ILogger<RagService> log, CancellationToken ct) =>
        {
            try
            {
                var count = await index.CountAsync(ct);
                return Results.Ok(new { enabled = opt.Value.Enabled, collection = opt.Value.Collection, points = count });
            }
            catch (Exception ex) { return Fail(log, "status", ex); }
        });

        group.MapPost("/index", async (bool? recreate, string? path, RagService svc, ILogger<RagService> log, CancellationToken ct) =>
        {
            try
            {
                var result = await svc.IndexAsync(recreate ?? false, path, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) { return Fail(log, "index", ex); }
        });

        group.MapGet("/search", async (string q, string? slug, int? limit, RagService svc, ILogger<RagService> log, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { error = "query 'q' is required" });
            try
            {
                var hits = await svc.SearchAsync(q, limit, slug, ct);
                return Results.Ok(hits);
            }
            catch (Exception ex) { return Fail(log, "search", ex); }
        });

        group.MapPost("/generate", async (GenerateRequest req, RagService svc, ILogger<RagService> log, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Topic)) return Results.BadRequest(new { error = "topic is required" });
            try
            {
                var question = await svc.GenerateQuestionAsync(req.Topic, req.Slug, ct);
                return Results.Ok(question);
            }
            catch (Exception ex) { return Fail(log, "generate", ex); }
        });

        group.MapPost("/explain", async (ExplainRequest req, RagService svc, ILogger<RagService> log, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Question)) return Results.BadRequest(new { error = "question is required" });
            try
            {
                var text = await svc.ExplainAsync(req.Question, req.CorrectAnswer ?? "", req.UserAnswer, req.Slug, ct);
                return Results.Ok(new { explanation = text });
            }
            catch (Exception ex) { return Fail(log, "explain", ex); }
        });

        return app;
    }

    private static IResult Fail(ILogger logger, string op, Exception ex)
    {
        logger.LogError(ex, "RAG operation '{Op}' failed", op);
        return Results.Problem(
            title: $"RAG {op} unavailable",
            detail: ex.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
