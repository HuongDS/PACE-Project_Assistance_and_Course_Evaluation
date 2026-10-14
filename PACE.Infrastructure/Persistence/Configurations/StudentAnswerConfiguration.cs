using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Infrastructure.Persistence.Configurations;

public class StudentAnswerConfiguration : IEntityTypeConfiguration<StudentAnswer>
{
    public void Configure(EntityTypeBuilder<StudentAnswer> e)
    {
        e.ToTable("student_answers");
        e.HasKey(x => x.Id);
        e.Property(x => x.FeedbackFormat).HasMaxLength(20);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        e.HasIndex(x => x.UserId);
        e.HasIndex(x => new { x.SubmissionId, x.QuestionId }).IsUnique()
            .HasFilter("\"SubmissionId\" IS NOT NULL");

        e.HasOne(x => x.Submission)
            .WithMany(s => s.Answers)
            .HasForeignKey(x => x.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.User)
            .WithMany(u => u.Answers)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Question)
            .WithMany(q => q.Answers)
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
