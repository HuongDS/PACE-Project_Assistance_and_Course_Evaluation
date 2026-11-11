using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> e)
    {
        e.ToTable("student_profiles");
        e.HasKey(x => x.StudentCode);

        e.Property(x => x.StudentCode).HasColumnName("student_code").HasMaxLength(20);
        e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        e.Property(x => x.EnrollmentYear).HasColumnName("enrollment_year").IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        e.HasIndex(x => x.UserId).IsUnique();

        e.HasOne(x => x.User)
            .WithOne(u => u.StudentProfile)
            .HasForeignKey<StudentProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LecturerProfileConfiguration : IEntityTypeConfiguration<LecturerProfile>
{
    public void Configure(EntityTypeBuilder<LecturerProfile> e)
    {
        e.ToTable("lecturer_profiles");
        e.HasKey(x => x.LecturerCode);

        e.Property(x => x.LecturerCode).HasColumnName("lecturer_code").HasMaxLength(20);
        e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        e.Property(x => x.AcademicTitle).HasColumnName("academic_title").HasMaxLength(100).IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        e.HasIndex(x => x.UserId).IsUnique();

        e.HasOne(x => x.User)
            .WithOne(u => u.LecturerProfile)
            .HasForeignKey<LecturerProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

