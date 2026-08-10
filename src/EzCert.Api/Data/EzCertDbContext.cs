using EzCert.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EzCert.Api.Data;

public class EzCertDbContext : DbContext
{
    public EzCertDbContext(DbContextOptions<EzCertDbContext> options) : base(options) { }

    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamSection> ExamSections => Set<ExamSection>();
    public DbSet<QuestionPool> QuestionPools => Set<QuestionPool>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Choice> Choices => Set<Choice>();
    public DbSet<ProcessedChunk> ProcessedChunks => Set<ProcessedChunk>();

    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<QuestionSnapshot> QuestionSnapshots => Set<QuestionSnapshot>();
    public DbSet<AnswerSubmission> AnswerSubmissions => Set<AnswerSubmission>();
    public DbSet<ScoreReport> ScoreReports => Set<ScoreReport>();
    public DbSet<SectionScore> SectionScores => Set<SectionScore>();
    public DbSet<Credential> Credentials => Set<Credential>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Store enums as text for readability.
        b.Entity<Question>().Property(q => q.Type).HasConversion<string>();
        b.Entity<Question>().Property(q => q.Difficulty).HasConversion<string>();
        b.Entity<ExamSession>().Property(s => s.Mode).HasConversion<string>();
        b.Entity<ExamSession>().Property(s => s.Status).HasConversion<string>();

        b.Entity<Certification>().HasIndex(c => c.Code).IsUnique();
        b.Entity<ExamSection>().HasIndex(s => new { s.ExamId, s.Slug }).IsUnique();
        b.Entity<Question>().HasIndex(q => new { q.QuestionPoolId, q.ExternalId }).IsUnique();
        b.Entity<Credential>().HasIndex(c => c.VerificationToken).IsUnique();

        // Cascade deletes down the catalog and session trees.
        b.Entity<Exam>().HasOne(e => e.Certification).WithMany(c => c.Exams)
            .HasForeignKey(e => e.CertificationId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ExamSection>().HasOne(s => s.Exam).WithMany(e => e.Sections)
            .HasForeignKey(s => s.ExamId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<QuestionPool>().HasOne(p => p.ExamSection).WithMany(s => s.Pools)
            .HasForeignKey(p => p.ExamSectionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Question>().HasOne(q => q.QuestionPool).WithMany(p => p.Questions)
            .HasForeignKey(q => q.QuestionPoolId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Choice>().HasOne(c => c.Question).WithMany(q => q.Choices)
            .HasForeignKey(c => c.QuestionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProcessedChunk>().HasOne(pc => pc.Certification).WithMany(c => c.Chunks)
            .HasForeignKey(pc => pc.CertificationId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<QuestionSnapshot>().HasOne(qs => qs.ExamSession).WithMany(s => s.Snapshots)
            .HasForeignKey(qs => qs.ExamSessionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AnswerSubmission>().HasOne(a => a.QuestionSnapshot).WithMany(qs => qs.Answers)
            .HasForeignKey(a => a.QuestionSnapshotId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ScoreReport>().HasOne(sr => sr.ExamSession).WithOne(s => s.ScoreReport)
            .HasForeignKey<ScoreReport>(sr => sr.ExamSessionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SectionScore>().HasOne(ss => ss.ScoreReport).WithMany(sr => sr.SectionScores)
            .HasForeignKey(ss => ss.ScoreReportId).OnDelete(DeleteBehavior.Cascade);
    }
}
