using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.DAL.Models;

namespace PACE.DAL.Data.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> e)
    {
        e.ToTable("businesses");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.BusinessName).IsRequired();

        e.HasOne(x => x.AnalysisRun)
            .WithMany(r => r.Businesses)
            .HasForeignKey(x => x.AnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
