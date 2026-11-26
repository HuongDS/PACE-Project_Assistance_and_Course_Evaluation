using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class CourseSyllabusConfiguration : IEntityTypeConfiguration<CourseSyllabus>
{
    public void Configure(EntityTypeBuilder<CourseSyllabus> entity)
    {
        entity.ToTable("course_syllabi");
        entity.HasKey(x => x.CourseId);
        entity.Property(x => x.CourseId).HasColumnName("course_id").ValueGeneratedNever();
        entity.Property(x => x.SourceId).HasColumnName("source_id").HasMaxLength(64);
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(255);
        entity.Property(x => x.ContentJson).HasColumnName("content").HasColumnType("jsonb");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.HasOne(x => x.Course).WithOne().HasForeignKey<CourseSyllabus>(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
