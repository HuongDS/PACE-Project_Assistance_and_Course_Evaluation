using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.DAL.Models;

namespace PACE.DAL.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.ToTable("users", t =>
            t.HasCheckConstraint("CK_users_StudentHasRepo",
                "\"Role\" = 'Admin' OR \"AnalysisRunId\" IS NOT NULL"));

        e.HasKey(x => x.Id);
        e.Property(x => x.Email).IsRequired().HasMaxLength(255);
        e.HasIndex(x => x.Email).IsUnique();

        e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        e.HasOne(x => x.AnalysisRun)
            .WithMany(r => r.Users)
            .HasForeignKey(x => x.AnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
