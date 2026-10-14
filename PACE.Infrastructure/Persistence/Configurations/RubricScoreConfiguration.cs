using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Infrastructure.Persistence.Configurations;

public class RubricScoreConfiguration : IEntityTypeConfiguration<RubricScore>
{
    public void Configure(EntityTypeBuilder<RubricScore> e)
    {
        e.ToTable("rubric_scores", t =>
            t.HasCheckConstraint("CK_rubric_scores_Score", "\"Score\" BETWEEN 1 AND 5"));
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.StudentAnswerId, x.CriterionId }).IsUnique();

        e.HasOne(x => x.StudentAnswer)
            .WithMany(a => a.RubricScores)
            .HasForeignKey(x => x.StudentAnswerId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Criterion)
            .WithMany()
            .HasForeignKey(x => x.CriterionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
