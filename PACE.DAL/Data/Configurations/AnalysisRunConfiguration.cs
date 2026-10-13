using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.DAL.Models;

namespace PACE.DAL.Data.Configurations;

public class AnalysisRunConfiguration : IEntityTypeConfiguration<AnalysisRun>
{
    public void Configure(EntityTypeBuilder<AnalysisRun> e)
    {
        e.ToTable("analysis_runs");
        e.HasKey(x => x.Id);
        e.Property(x => x.RepoUrl).HasMaxLength(500);
        e.Property(x => x.RepoName).HasMaxLength(255);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
    }
}
