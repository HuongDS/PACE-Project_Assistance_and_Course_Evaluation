using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Infrastructure.Persistence.Configurations;

public class BusinessFlowConfiguration : IEntityTypeConfiguration<BusinessFlow>
{
    public void Configure(EntityTypeBuilder<BusinessFlow> e)
    {
        e.ToTable("business_flows");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).IsRequired().HasMaxLength(500);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        e.HasOne(x => x.Business)
            .WithMany(b => b.Flows)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.EntryMethod)
            .WithMany()
            .HasForeignKey(x => x.EntryMethodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
