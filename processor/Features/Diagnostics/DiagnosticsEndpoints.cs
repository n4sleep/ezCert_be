using System.Diagnostics;
using EzCert.Processor.Infrastructure.Bedrock;

namespace EzCert.Processor.Features.Diagnostics;

// Dev/ops diagnostics: exercises the active IBedrockClient (gateway or direct)
// end-to-end so quality can be verified without a UI. Enabled only when
// Diagnostics:Enabled=true.
public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/diagnostics/bedrock", async (IBedrockClient bedrock, ILogger<IBedrockClient> log, CancellationToken ct) =>
        {
            var results = new List<object>();
            var failures = new List<string>();

            var sw = Stopwatch.StartNew();
            try
            {
                var embedding = await bedrock.EmbedAsync("What is cloud computing?", ct);
                results.Add(new { op = "embed", ok = embedding.Length > 0, dims = embedding.Length, ms = sw.ElapsedMilliseconds });
                log.LogInformation("diagnostics embed: {Dims} dims in {Ms}ms", embedding.Length, sw.ElapsedMilliseconds);
            }
            catch (Exception ex) { failures.Add($"embed: {ex.Message}"); log.LogError(ex, "diagnostics embed failed"); }

            sw.Restart();
            try
            {
                var text = await bedrock.GenerateAsync(
                    "Explain in one sentence what IaaS means.",
                    "You are a concise cloud tutor.",
                    maxTokens: 100, temperature: 0.3f, ct: ct);
                results.Add(new { op = "generate", ok = !string.IsNullOrWhiteSpace(text), chars = text.Length, ms = sw.ElapsedMilliseconds });
            }
            catch (Exception ex) { failures.Add($"generate: {ex.Message}"); log.LogError(ex, "diagnostics generate failed"); }

            sw.Restart();
            try
            {
                var text = await bedrock.ExplainAsync(
                    "What is IaaS?", "Infrastructure as a Service", "Software as a Service", null, ct);
                results.Add(new { op = "explain", ok = !string.IsNullOrWhiteSpace(text), chars = text.Length, ms = sw.ElapsedMilliseconds });
            }
            catch (Exception ex) { failures.Add($"explain: {ex.Message}"); log.LogError(ex, "diagnostics explain failed"); }

            var clientType = bedrock.GetType().Name;
            return Results.Ok(new
            {
                client = clientType,
                results,
                failures,
                allOk = failures.Count == 0 && results.Count == 3,
            });
        }).WithTags("Diagnostics");

        return app;
    }
}
