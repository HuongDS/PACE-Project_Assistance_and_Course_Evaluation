using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;
namespace PACE.Infrastructure.Persistence.Configurations;
public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> e)
    {
        e.ToTable("courses"); e.HasKey(x => x.Id);
        e.Property(x => x.Code).HasMaxLength(40); e.Property(x => x.Name).HasMaxLength(255);
        e.Property(x => x.Credits).HasColumnName("credits").HasDefaultValue(3);
        e.HasIndex(x => x.Code).IsUnique();
    }
}
public class CourseProjectConfiguration : IEntityTypeConfiguration<CourseProject>
{
    public void Configure(EntityTypeBuilder<CourseProject> e)
    {
        e.ToTable("course_projects"); e.HasKey(x => x.AnalysisRunId); e.Property(x => x.Notes).HasMaxLength(4000);
        e.HasOne(x => x.Course).WithMany(x => x.Projects).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.AnalysisRun).WithMany().HasForeignKey(x => x.AnalysisRunId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class AssessmentSessionConfiguration : IEntityTypeConfiguration<AssessmentSession>
{
    public void Configure(EntityTypeBuilder<AssessmentSession> e)
    {
        e.ToTable("assessment_sessions", t => t.HasCheckConstraint("CK_session_status", "\"Status\" IN ('CREATED','IN_PROGRESS','COMPLETED')")); e.HasKey(x => x.Id);
        e.Property(x => x.Title).HasMaxLength(255); e.Property(x => x.Status).HasMaxLength(20); e.Property(x => x.Version).IsConcurrencyToken();
        e.Property(x => x.OralExamId).HasColumnName("oral_exam_id");
        e.Property(x => x.IsIndividual).HasColumnName("is_individual");
        e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.OralExam).WithMany(o => o.AssessmentSessions).HasForeignKey(x => x.OralExamId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.AssessmentRoundRevision).WithMany().HasForeignKey(x => x.AssessmentRoundRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.ProjectId, x.ScheduledStart });
    }
}
public class SessionStudentConfiguration : IEntityTypeConfiguration<SessionStudent>
{
    public void Configure(EntityTypeBuilder<SessionStudent> e)
    {
        e.ToTable("session_students"); e.HasKey(x => new { x.SessionId, x.StudentId });
        e.HasOne(x => x.Session).WithMany(x => x.Students).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class IndividualOralSetupConfiguration : IEntityTypeConfiguration<IndividualOralSetup>
{
    public void Configure(EntityTypeBuilder<IndividualOralSetup> e)
    {
        e.ToTable("individual_oral_setups"); e.HasKey(x => x.SessionId);
        e.Property(x => x.SessionId).HasColumnName("session_id").ValueGeneratedNever();
        e.Property(x => x.DurationMinutes).HasColumnName("duration_minutes");
        e.Property(x => x.QuestionsPerStudent).HasColumnName("questions_per_student");
        e.Property(x => x.Difficulty).HasColumnName("difficulty").HasMaxLength(20);
        e.Property(x => x.CodeDeadline).HasColumnName("code_deadline");
        e.Property(x => x.CommitSha).HasColumnName("commit_sha").HasMaxLength(64);
        e.Property(x => x.CommitAt).HasColumnName("commit_at");
        e.Property(x => x.ScannedAt).HasColumnName("scanned_at");
        e.Property(x => x.SnapshotJson).HasColumnName("snapshot_json");
        e.HasOne(x => x.Session).WithOne(x => x.IndividualSetup).HasForeignKey<IndividualOralSetup>(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class OralAttemptConfiguration : IEntityTypeConfiguration<OralAttempt>
{
    public void Configure(EntityTypeBuilder<OralAttempt> e)
    {
        e.ToTable("oral_attempts"); e.HasKey(x => new { x.SessionId, x.StudentId });
        e.Property(x => x.SessionId).HasColumnName("session_id");
        e.Property(x => x.StudentId).HasColumnName("student_id");
        e.Property(x => x.QuestionsJson).HasColumnName("questions_json");
        e.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
        e.Property(x => x.StartedAt).HasColumnName("started_at");
        e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        e.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
        e.Property(x => x.AnswersJson).HasColumnName("answers_json");
        e.Property(x => x.AiReviewJson).HasColumnName("ai_review_json");
        e.Property(x => x.AiReviewedAt).HasColumnName("ai_reviewed_at");
        e.Property(x => x.HistoryJson).HasColumnName("history_json");
        e.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        e.HasOne(x => x.Student).WithOne().HasForeignKey<OralAttempt>(x => new { x.SessionId, x.StudentId }).OnDelete(DeleteBehavior.Cascade);
    }
}
public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> e)
    {
        e.ToTable("audit_events"); e.HasKey(x => x.Id); e.Property(x => x.ActorName).HasMaxLength(255);
        e.Property(x => x.EventType).HasMaxLength(80); e.Property(x => x.Details).HasMaxLength(4000);
        e.HasIndex(x => new { x.ResourceId, x.Timestamp });
    }
}
