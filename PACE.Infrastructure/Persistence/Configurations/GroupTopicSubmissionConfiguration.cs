using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class GroupTopicSubmissionConfiguration : IEntityTypeConfiguration<GroupTopicSubmission>
{
    public void Configure(EntityTypeBuilder<GroupTopicSubmission> entity)
    {
        entity.ToTable("group_topic_submissions");
        entity.HasKey(x => new { x.GroupId, x.Version });
        entity.Property(x => x.Title).HasMaxLength(255);
        entity.Property(x => x.Description).HasMaxLength(4000);
        entity.Property(x => x.Notes).HasMaxLength(4000);
        entity.Property(x => x.ProposalFileName).HasMaxLength(255);
        entity.Property(x => x.ProposalContentType).HasMaxLength(100);
        entity.Property(x => x.ProposalFile).HasColumnType("bytea");
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsConcurrencyToken();
        entity.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId);
    }
}
