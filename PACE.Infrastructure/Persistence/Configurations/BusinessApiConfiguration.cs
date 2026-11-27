using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class BusinessApiConfiguration : IEntityTypeConfiguration<BusinessApi>
{
    public void Configure(EntityTypeBuilder<BusinessApi> e)
    {
        e.ToTable("business_apis");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.BusinessId).HasColumnName("business_id").IsRequired();
        e.Property(x => x.Controller).HasColumnName("controller").HasMaxLength(255).IsRequired();
        e.Property(x => x.Function).HasColumnName("function").HasMaxLength(255).IsRequired();
        e.Property(x => x.HttpMethod).HasColumnName("http_method").HasMaxLength(10);
        e.Property(x => x.Path).HasColumnName("path").HasMaxLength(500);
        e.Property(x => x.OrderIndex).HasColumnName("order_index").HasDefaultValue(0);

        e.HasOne(x => x.Business)
            .WithMany(b => b.Apis)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => new { x.BusinessId, x.Controller, x.Function }).IsUnique();
    }
}
