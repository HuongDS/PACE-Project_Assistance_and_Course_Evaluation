using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.DAL.Models;

namespace PACE.DAL.Data.Configurations;

public class BusinessMethodConfiguration : IEntityTypeConfiguration<BusinessMethod>
{
    public void Configure(EntityTypeBuilder<BusinessMethod> e)
    {
        e.ToTable("business_methods");
        e.HasKey(x => new { x.BusinessId, x.MethodSourceId });

        e.HasOne(x => x.Business)
            .WithMany(b => b.BusinessMethods)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.MethodSource)
            .WithMany(m => m.BusinessMethods)
            .HasForeignKey(x => x.MethodSourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
