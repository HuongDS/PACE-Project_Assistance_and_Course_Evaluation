using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class SemesterConfiguration : IEntityTypeConfiguration<Semester>
{
    public void Configure(EntityTypeBuilder<Semester> e)
    {
        e.ToTable("semesters");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        e.Property(x => x.AcademicYear).HasColumnName("academic_year").HasMaxLength(20).IsRequired();
        e.Property(x => x.StartDate).HasColumnName("start_date").IsRequired();
        e.Property(x => x.ActualStartDate).HasColumnName("actual_start_date").IsRequired();
        e.Property(x => x.EndDate).HasColumnName("end_date").IsRequired();
    }
}

public class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> e)
    {
        e.ToTable("classes");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.CourseId).HasColumnName("course_id").IsRequired();
        e.Property(x => x.SemesterId).HasColumnName("semester_id");
        e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
        e.Property(x => x.StartDate).HasColumnName("start_date").IsRequired();
        e.Property(x => x.ActualStartDate).HasColumnName("actual_start_date").IsRequired();
        e.Property(x => x.EndDate).HasColumnName("end_date").IsRequired();

        e.HasOne(x => x.Course)
            .WithMany(c => c.Classes)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Semester)
            .WithMany(s => s.Classes)
            .HasForeignKey(x => x.SemesterId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class StudentClassConfiguration : IEntityTypeConfiguration<StudentClass>
{
    public void Configure(EntityTypeBuilder<StudentClass> e)
    {
        e.ToTable("student_classes");
        e.HasKey(x => new { x.StudentCode, x.ClassId });

        e.Property(x => x.StudentCode).HasColumnName("student_code").HasMaxLength(20);
        e.Property(x => x.ClassId).HasColumnName("class_id");
        e.Property(x => x.Status).HasColumnName("status").HasDefaultValue(1);

        e.HasOne(x => x.StudentProfile)
            .WithMany(s => s.StudentClasses)
            .HasForeignKey(x => x.StudentCode)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Class)
            .WithMany(c => c.StudentClasses)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

