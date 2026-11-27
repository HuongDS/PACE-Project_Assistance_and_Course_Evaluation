using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class CourseSyllabusConfiguration : IEntityTypeConfiguration<CourseSyllabus>
{
    public void Configure(EntityTypeBuilder<CourseSyllabus> entity)
    {
        entity.ToTable("course_syllabi");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(x => x.CourseId).HasColumnName("course_id");
        entity.Property(x => x.SemesterId).HasColumnName("semester_id");
        entity.Property(x => x.SourceId).HasColumnName("source_id").HasMaxLength(64);
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(255);
        entity.Property(x => x.ContentJson).HasColumnName("content").HasColumnType("jsonb");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.HasIndex(x => new { x.CourseId, x.SemesterId }).IsUnique();
        entity.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Semester).WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
    }
}
