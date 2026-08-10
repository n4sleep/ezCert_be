using EzCert.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EzCert.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "ezcert-api",
            time = DateTime.UtcNow
        }))
        .WithName("Health")
        .WithTags("Health");

        app.MapGet("/health/db", async (EzCertDbContext db, CancellationToken ct) =>
        {
            var canConnect = await db.Database.CanConnectAsync(ct);
            var payload = new { status = canConnect ? "ok" : "unavailable", database = canConnect };
            return canConnect ? Results.Ok(payload) : Results.Json(payload, statusCode: 503);
        })
        .WithName("HealthDb")
        .WithTags("Health");

        return app;
    }
}
