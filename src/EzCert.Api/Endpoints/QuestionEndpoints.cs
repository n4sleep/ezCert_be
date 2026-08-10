using EzCert.Api.Contracts;
using EzCert.Api.Data;
using EzCert.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EzCert.Api.Endpoints;

// Question Pool retrieval (Story 2.5).
public static class QuestionEndpoints
{
    public static IEndpointRouteBuilder MapQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog").WithTags("Questions");

        group.MapGet("/question-pools/{poolId:guid}/questions", async (
            Guid poolId, string? difficulty, EzCertDbContext db, CancellationToken ct) =>
        {
            if (!await db.QuestionPools.AnyAsync(p => p.Id == poolId, ct))
                return Results.NotFound();

            var q = db.Questions.Include(x => x.Choices).AsNoTracking().Where(x => x.QuestionPoolId == poolId);
            if (!string.IsNullOrWhiteSpace(difficulty) && Enum.TryParse<Difficulty>(difficulty, true, out var d))
                q = q.Where(x => x.Difficulty == d);

            var items = await q.ToListAsync(ct);
            return Results.Ok(items.Select(ToDto));
        });

        // Convenience: all questions for a section slug within an exam.
        group.MapGet("/exams/{examId:guid}/sections/{slug}/questions", async (
            Guid examId, string slug, EzCertDbContext db, CancellationToken ct) =>
        {
            var section = await db.ExamSections
                .Include(s => s.Pools).ThenInclude(p => p.Questions).ThenInclude(q => q.Choices)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ExamId == examId && s.Slug == slug, ct);
            if (section is null) return Results.NotFound();

            var items = section.Pools.SelectMany(p => p.Questions).Select(ToDto);
            return Results.Ok(items);
        });

        return app;
    }

    private static QuestionDto ToDto(Question q) => new(
        q.Id,
        q.ExternalId,
        q.Type.ToString(),
        q.Difficulty.ToString(),
        q.Text,
        q.Explanation,
        q.Source,
        q.Choices.OrderBy(c => c.Ordinal)
            .Select(c => new ChoiceDto(c.Label, c.Text, c.IsCorrect, c.Ordinal)).ToList());
}
