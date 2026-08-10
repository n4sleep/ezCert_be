using EzCert.Api.Contracts;
using EzCert.Api.Data;
using EzCert.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EzCert.Api.Endpoints;

// Catalog Management (Story 2.2): Certification -> Exam -> ExamSection CRUD.
public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog").WithTags("Catalog");

        group.MapGet("/certifications", async (EzCertDbContext db, CancellationToken ct) =>
        {
            var certs = await db.Certifications
                .Include(c => c.Exams).ThenInclude(e => e.Sections)
                .AsNoTracking()
                .OrderBy(c => c.Code)
                .ToListAsync(ct);
            return Results.Ok(certs.Select(ToDto));
        });

        group.MapGet("/certifications/{id:guid}", async (Guid id, EzCertDbContext db, CancellationToken ct) =>
        {
            var cert = await db.Certifications
                .Include(c => c.Exams).ThenInclude(e => e.Sections)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);
            return cert is null ? Results.NotFound() : Results.Ok(ToDto(cert));
        });

        group.MapPost("/certifications", async (CreateCertificationRequest req, EzCertDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "Code and Name are required." });
            if (await db.Certifications.AnyAsync(c => c.Code == req.Code, ct))
                return Results.Conflict(new { error = $"Certification '{req.Code}' already exists." });

            var cert = new Certification { Code = req.Code, Name = req.Name, Description = req.Description };
            db.Certifications.Add(cert);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/catalog/certifications/{cert.Id}", ToDto(cert));
        });

        group.MapPut("/certifications/{id:guid}", async (Guid id, UpdateCertificationRequest req, EzCertDbContext db, CancellationToken ct) =>
        {
            var cert = await db.Certifications.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (cert is null) return Results.NotFound();
            cert.Name = req.Name;
            cert.Description = req.Description;
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(cert));
        });

        group.MapDelete("/certifications/{id:guid}", async (Guid id, EzCertDbContext db, CancellationToken ct) =>
        {
            var cert = await db.Certifications.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (cert is null) return Results.NotFound();
            db.Certifications.Remove(cert);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapPost("/certifications/{id:guid}/exams", async (Guid id, CreateExamRequest req, EzCertDbContext db, CancellationToken ct) =>
        {
            var cert = await db.Certifications.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (cert is null) return Results.NotFound();
            var exam = new Exam
            {
                CertificationId = id,
                Name = req.Name,
                PassPercent = req.PassPercent is >= 0 and <= 100 ? req.PassPercent : 70,
                TimeLimitMinutes = req.TimeLimitMinutes
            };
            db.Exams.Add(exam);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/catalog/exams/{exam.Id}", new ExamDto(exam.Id, exam.Name, exam.PassPercent, exam.TimeLimitMinutes, Array.Empty<SectionDto>()));
        });

        group.MapPost("/exams/{examId:guid}/sections", async (Guid examId, CreateSectionRequest req, EzCertDbContext db, CancellationToken ct) =>
        {
            var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == examId, ct);
            if (exam is null) return Results.NotFound();
            if (await db.ExamSections.AnyAsync(s => s.ExamId == examId && s.Slug == req.Slug, ct))
                return Results.Conflict(new { error = $"Section '{req.Slug}' already exists on this exam." });

            var section = new ExamSection { ExamId = examId, Slug = req.Slug, Name = req.Name, Ordinal = req.Ordinal };
            db.ExamSections.Add(section);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/catalog/sections/{section.Id}", new SectionDto(section.Id, section.Slug, section.Name, section.Ordinal));
        });

        return app;
    }

    private static CertificationDto ToDto(Certification c) => new(
        c.Id, c.Code, c.Name, c.Description,
        c.Exams.Select(e => new ExamDto(
            e.Id, e.Name, e.PassPercent, e.TimeLimitMinutes,
            e.Sections.OrderBy(s => s.Ordinal)
                .Select(s => new SectionDto(s.Id, s.Slug, s.Name, s.Ordinal)).ToList()))
            .ToList());
}
