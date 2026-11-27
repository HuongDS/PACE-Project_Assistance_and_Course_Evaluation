using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Infrastructure.Persistence.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> e)
    {
        e.ToTable("businesses");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.BusinessName).IsRequired();
        e.Property(x => x.OrderIndex).HasDefaultValue(0);
        e.Property(x => x.OwnerStudentCode).HasMaxLength(20);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        e.HasOne(x => x.AnalysisRun)
            .WithMany(r => r.Businesses)
            .HasForeignKey(x => x.AnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerStudentCode)
            .OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => new { x.AnalysisRunId, x.OrderIndex });
    }
}
