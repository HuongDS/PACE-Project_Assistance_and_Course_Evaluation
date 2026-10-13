using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.DAL.Models;

namespace PACE.DAL.Data.Configurations;

public class DataFlowEdgeConfiguration : IEntityTypeConfiguration<DataFlowEdge>
{
    public void Configure(EntityTypeBuilder<DataFlowEdge> e)
    {
        e.ToTable("data_flow_edges");
        e.HasKey(x => x.Id);
        e.Property(x => x.EdgeType).HasConversion<string>().HasMaxLength(10);
        e.HasIndex(x => new { x.FlowId, x.StepOrder }).IsUnique();

        e.HasOne(x => x.Flow)
            .WithMany(f => f.Edges)
            .HasForeignKey(x => x.FlowId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.FromMethod)
            .WithMany(m => m.OutgoingEdges)
            .HasForeignKey(x => x.FromMethodId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.ToMethod)
            .WithMany(m => m.IncomingEdges)
            .HasForeignKey(x => x.ToMethodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
