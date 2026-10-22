using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Infrastructure.Persistence.Configurations;

public class AnalysisRunConfiguration : IEntityTypeConfiguration<AnalysisRun>
{
    public void Configure(EntityTypeBuilder<AnalysisRun> e)
    {
        e.ToTable("analysis_runs");
        e.HasKey(x => x.Id);
        e.Property(x => x.RepoUrl).HasMaxLength(500);
        e.Property(x => x.AnalyzedRepoUrl).HasMaxLength(500);
        e.Property(x => x.RepoName).HasMaxLength(255);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        e.Property(x => x.BusinessTemplateJson).HasColumnType("jsonb");
        e.Property(x => x.TemplateSource).HasConversion<string>().HasMaxLength(20);
    }
}
