using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.DAL.Models;

namespace PACE.DAL.Data.Configurations;

public class StudentAnswerConfiguration : IEntityTypeConfiguration<StudentAnswer>
{
    public void Configure(EntityTypeBuilder<StudentAnswer> e)
    {
        e.ToTable("student_answers");
        e.HasKey(x => x.Id);
        e.Property(x => x.FeedbackFormat).HasMaxLength(20);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        e.HasIndex(x => new { x.UserId, x.QuestionId }).IsUnique();

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
