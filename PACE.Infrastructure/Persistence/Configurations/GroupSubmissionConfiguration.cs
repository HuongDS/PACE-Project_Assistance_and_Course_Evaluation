using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class GroupSubmissionConfiguration : IEntityTypeConfiguration<GroupSubmission>
{
    public void Configure(EntityTypeBuilder<GroupSubmission> entity)
    {
        entity.ToTable("group_submissions");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(x => x.GroupId).HasColumnName("group_id");
        entity.Property(x => x.MilestoneId).HasColumnName("milestone_id");
        entity.Property(x => x.Deliverable).HasColumnName("deliverable").HasMaxLength(255);
        entity.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255);
        entity.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(127);
        entity.Property(x => x.FileSize).HasColumnName("file_size");
        entity.Property(x => x.Content).HasColumnName("content");
        entity.Property(x => x.StorageKey).HasColumnName("storage_key").HasMaxLength(255);
        entity.Property(x => x.Url).HasColumnName("url").HasMaxLength(2000);
        entity.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        entity.Property(x => x.SubmittedBy).HasColumnName("submitted_by");
        entity.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
        entity.HasIndex(x => new { x.GroupId, x.MilestoneId });
        entity.HasIndex(x => x.MilestoneId);
        entity.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        // Xóa mốc thì tài liệu vẫn còn, chuyển sang không gắn mốc.
        entity.HasOne(x => x.Milestone).WithMany().HasForeignKey(x => x.MilestoneId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class GroupSubmissionReviewConfiguration : IEntityTypeConfiguration<GroupSubmissionReview>
{
    public void Configure(EntityTypeBuilder<GroupSubmissionReview> entity)
    {
        entity.ToTable("group_submission_reviews");
        entity.HasKey(x => new { x.GroupId, x.MilestoneId });
        entity.Property(x => x.GroupId).HasColumnName("group_id");
        entity.Property(x => x.MilestoneId).HasColumnName("milestone_id");
        entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.Score).HasColumnName("score").HasPrecision(4, 2);
        entity.Property(x => x.Feedback).HasColumnName("feedback").HasMaxLength(4000);
        entity.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
        entity.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
        entity.HasIndex(x => x.MilestoneId);
        entity.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Milestone).WithMany().HasForeignKey(x => x.MilestoneId).OnDelete(DeleteBehavior.Cascade);
    }
}
