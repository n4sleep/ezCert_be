using EzCert.Processor.Features.Attempts;
using EzCert.Processor.Features.Exams;
using EzCert.Processor.Features.Generation;
using EzCert.Processor.Features.Guests;
using EzCert.Processor.Features.Sources;
using Microsoft.EntityFrameworkCore;

namespace EzCert.Processor.Infrastructure.Postgres;

public class EzCertDbContext : DbContext
{
    public EzCertDbContext(DbContextOptions<EzCertDbContext> options) : base(options) { }

    public DbSet<GuestDevice> GuestDevices => Set<GuestDevice>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<SourceDocument> SourceDocuments => Set<SourceDocument>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamSource> ExamSources => Set<ExamSource>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Choice> Choices => Set<Choice>();
    public DbSet<QuestionCitation> QuestionCitations => Set<QuestionCitation>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<AttemptQuestion> AttemptQuestions => Set<AttemptQuestion>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<SectionScore> SectionScores => Set<SectionScore>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Exam>().Property(e => e.Mode).HasConversion<string>();
        b.Entity<Exam>().Property(e => e.Status).HasConversion<string>();
        b.Entity<Question>().Property(q => q.Type).HasConversion<string>();
        b.Entity<Attempt>().Property(a => a.Status).HasConversion<string>();
        b.Entity<Source>().Property(s => s.Kind).HasConversion<string>();
        b.Entity<Source>().Property(s => s.Status).HasConversion<string>();
        b.Entity<ProcessingJob>().Property(j => j.Kind).HasConversion<string>();
        b.Entity<ProcessingJob>().Property(j => j.Status).HasConversion<string>();

        b.Entity<GuestDevice>().HasIndex(g => g.DeviceId).IsUnique();
        b.Entity<Exam>().HasIndex(e => e.ShareToken).IsUnique();
        b.Entity<Question>().HasIndex(q => new { q.ExamId, q.Ordinal }).IsUnique();
        b.Entity<Attempt>().HasIndex(a => new { a.ExamId, a.DeviceId });

        b.Entity<ExamSource>().HasKey(es => new { es.ExamId, es.SourceId });
        b.Entity<ExamSource>().HasOne(es => es.Exam).WithMany(e => e.Sources)
            .HasForeignKey(es => es.ExamId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Exam>().HasMany(e => e.Questions).WithOne(q => q.Exam)
            .HasForeignKey(q => q.ExamId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Exam>().HasMany(e => e.Attempts).WithOne(a => a.Exam)
            .HasForeignKey(a => a.ExamId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Question>().HasMany(q => q.Choices).WithOne(c => c.Question)
            .HasForeignKey(c => c.QuestionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Question>().HasMany(q => q.Citations).WithOne(c => c.Question)
            .HasForeignKey(c => c.QuestionId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Attempt>().HasMany(a => a.Questions).WithOne(q => q.Attempt)
            .HasForeignKey(q => q.AttemptId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Attempt>().HasMany(a => a.SectionScores).WithOne(s => s.Attempt)
            .HasForeignKey(s => s.AttemptId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AttemptQuestion>().HasOne(q => q.Answer).WithOne(a => a.AttemptQuestion)
            .HasForeignKey<Answer>(a => a.AttemptQuestionId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Source>().HasMany(s => s.Documents).WithOne(d => d.Source)
            .HasForeignKey(d => d.SourceId).OnDelete(DeleteBehavior.Cascade);
    }
}
