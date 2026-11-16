using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class ClassMilestoneConfiguration : IEntityTypeConfiguration<ClassMilestone>
{
    public void Configure(EntityTypeBuilder<ClassMilestone> entity)
    {
        entity.ToTable("class_milestones");
        entity.Property(x => x.Title).HasMaxLength(255);
        entity.Property(x => x.Description).HasMaxLength(4000);
        entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16).IsConcurrencyToken();
        entity.HasIndex(x => new { x.ClassId, x.DueAt });
        entity.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId);
    }
}

public class ClassAssessmentRoundConfiguration : IEntityTypeConfiguration<ClassAssessmentRound>
{
    public void Configure(EntityTypeBuilder<ClassAssessmentRound> entity)
    {
        entity.ToTable("class_assessment_rounds");
        entity.Property(x => x.Title).HasMaxLength(255);
        entity.Property(x => x.Weight).HasPrecision(5, 2);
        entity.Property(x => x.ConfigurationJson).HasColumnType("jsonb");
        entity.HasOne(x => x.Milestone).WithMany().HasForeignKey(x => x.MilestoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CourseAnnouncementConfiguration : IEntityTypeConfiguration<CourseAnnouncement>
{
    public void Configure(EntityTypeBuilder<CourseAnnouncement> entity)
    {
        entity.ToTable("course_announcements");
        entity.Property(x => x.Title).HasMaxLength(255);
        entity.Property(x => x.Body).HasMaxLength(8000);
        entity.HasIndex(x => new { x.ClassId, x.CreatedAt });
        entity.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId);
    }
}

public class AssessmentRoundRevisionConfiguration : IEntityTypeConfiguration<AssessmentRoundRevision>
{
    public void Configure(EntityTypeBuilder<AssessmentRoundRevision> entity)
    {
        entity.ToTable("assessment_round_revisions");
        entity.Property(x => x.Title).HasMaxLength(255);
        entity.Property(x => x.Weight).HasPrecision(5, 2);
        entity.Property(x => x.ConfigurationJson).HasColumnType("jsonb");
        entity.HasIndex(x => new { x.RoundId, x.Version }).IsUnique();
        entity.HasOne(x => x.Round).WithMany().HasForeignKey(x => x.RoundId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ClassScheduleImportConfiguration : IEntityTypeConfiguration<ClassScheduleImport>
{
    public const string PrimaryKey = "PK_class_schedule_imports";

    public void Configure(EntityTypeBuilder<ClassScheduleImport> entity)
    {
        entity.ToTable("class_schedule_imports");
        entity.HasKey(x => new { x.ClassId, x.Key }).HasName(PrimaryKey);
        entity.Property(x => x.Key).HasMaxLength(132);
        entity.Property(x => x.Fingerprint).HasMaxLength(72);
        entity.Property(x => x.ResultJson).HasColumnType("jsonb");
        entity.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId);
    }
}
