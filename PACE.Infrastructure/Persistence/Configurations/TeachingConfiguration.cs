using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;
namespace PACE.Infrastructure.Persistence.Configurations;
public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> e)
    {
        e.ToTable("courses"); e.HasKey(x => x.Id);
        e.Property(x => x.Code).HasMaxLength(40); e.Property(x => x.Name).HasMaxLength(255); e.Property(x => x.Semester).HasMaxLength(40);
        e.Property(x => x.Credits).HasColumnName("credits").HasDefaultValue(3);
        e.HasIndex(x => new { x.Code, x.Semester, x.LecturerId }).IsUnique();
        e.HasOne(x => x.Lecturer).WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
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
        e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.OralExam).WithMany(o => o.AssessmentSessions).HasForeignKey(x => x.OralExamId).OnDelete(DeleteBehavior.SetNull);
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
public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> e)
    {
        e.ToTable("audit_events"); e.HasKey(x => x.Id); e.Property(x => x.ActorName).HasMaxLength(255);
        e.Property(x => x.EventType).HasMaxLength(80); e.Property(x => x.Details).HasMaxLength(4000);
        e.HasIndex(x => new { x.ResourceId, x.Timestamp });
    }
}
