using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class BusinessDiagramConfiguration : IEntityTypeConfiguration<BusinessDiagram>
{
    public void Configure(EntityTypeBuilder<BusinessDiagram> e)
    {
        e.ToTable("business_diagrams");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.BusinessId).HasColumnName("business_id").IsRequired();
        e.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        e.Property(x => x.Format).HasColumnName("format").HasConversion<string>().HasMaxLength(20).IsRequired();
        e.Property(x => x.SourceXml).HasColumnName("source_xml");
        e.Property(x => x.SpecificationJson).HasColumnName("specification_json").HasColumnType("jsonb");
        e.Property(x => x.PreviewUrl).HasColumnName("preview_url").HasMaxLength(1000);
        e.Property(x => x.Description).HasColumnName("description");
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        e.HasOne(x => x.Business)
            .WithMany(b => b.Diagrams)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => new { x.BusinessId, x.Title });
    }
}
