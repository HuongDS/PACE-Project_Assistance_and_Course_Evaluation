using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class StudentSubmissionConfiguration : IEntityTypeConfiguration<StudentSubmission>
{
    public void Configure(EntityTypeBuilder<StudentSubmission> e)
    {
        e.ToTable("student_submissions");
        e.HasKey(x => x.Id);
        e.Property(x => x.SubmittedAt).HasDefaultValueSql("now()");

        e.HasOne(x => x.User)
            .WithMany(u => u.Submissions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.AnalysisRun)
            .WithMany(r => r.Submissions)
            .HasForeignKey(x => x.AnalysisRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
