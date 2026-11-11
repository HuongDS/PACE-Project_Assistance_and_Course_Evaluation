using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.ToTable("users");

        e.HasKey(x => x.Id);
        e.Property(x => x.Email).IsRequired().HasMaxLength(255);
        e.HasIndex(x => x.Email).IsUnique();

        e.Property(x => x.FullName).HasColumnName("full_name").IsRequired().HasMaxLength(255);
        e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
        e.Property(x => x.RefreshToken).HasColumnName("refresh_token").HasMaxLength(500);
        e.Property(x => x.RefreshTokenExpiryTime).HasColumnName("refresh_token_expiry_time");
        e.Property(x => x.Address).HasColumnName("address");
        e.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);

        e.Property(x => x.StudentCode).HasMaxLength(20);
        e.HasIndex(x => x.StudentCode).IsUnique()
            .HasFilter("\"StudentCode\" IS NOT NULL");

        e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        e.HasOne(x => x.AnalysisRun)
            .WithMany(r => r.Users)
            .HasForeignKey(x => x.AnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.StudentProfile)
            .WithOne(s => s.User)
            .HasForeignKey<StudentProfile>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.LecturerProfile)
            .WithOne(l => l.User)
            .HasForeignKey<LecturerProfile>(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
