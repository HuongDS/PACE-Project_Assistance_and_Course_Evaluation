using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.DAL.Models;

namespace PACE.DAL.Data.Configurations;

public class MethodSourceConfiguration : IEntityTypeConfiguration<MethodSource>
{
    public void Configure(EntityTypeBuilder<MethodSource> e)
    {
        e.ToTable("method_sources");
        e.HasKey(x => x.Id);
        e.Property(x => x.FilePath).HasMaxLength(500);
        e.Property(x => x.ClassName).IsRequired().HasMaxLength(255);
        e.Property(x => x.MethodName).IsRequired().HasMaxLength(255);
        e.Property(x => x.SourceCode).IsRequired();
        e.Property(x => x.Language).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.NodeType).HasConversion<string>().HasMaxLength(30);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        e.HasIndex(x => new { x.AnalysisRunId, x.ClassName, x.MethodName });

        e.HasOne(x => x.AnalysisRun)
            .WithMany(r => r.MethodSources)
            .HasForeignKey(x => x.AnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
