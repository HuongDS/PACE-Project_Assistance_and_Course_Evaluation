using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> e)
    {
        e.ToTable("rooms");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        e.Property(x => x.Building).HasColumnName("building").HasMaxLength(100);

        e.HasIndex(x => x.Code).IsUnique();
    }
}

public class TimeSlotConfiguration : IEntityTypeConfiguration<TimeSlot>
{
    public void Configure(EntityTypeBuilder<TimeSlot> e)
    {
        e.ToTable("time_slots");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.PeriodNumber).HasColumnName("period_number").IsRequired();
        e.Property(x => x.StartTime).HasColumnName("start_time").IsRequired();
        e.Property(x => x.EndTime).HasColumnName("end_time").IsRequired();
    }
}

public class ClassScheduleConfiguration : IEntityTypeConfiguration<ClassSchedule>
{
    public void Configure(EntityTypeBuilder<ClassSchedule> e)
    {
        e.ToTable("class_schedules");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.ClassId).HasColumnName("class_id").IsRequired();
        e.Property(x => x.LecturerCode).HasColumnName("lecturer_code").HasMaxLength(20).IsRequired();
        e.Property(x => x.RoomId).HasColumnName("room_id").IsRequired();
        e.Property(x => x.TimeSlotId).HasColumnName("time_slot_id").IsRequired();
        e.Property(x => x.DayOfWeek).HasColumnName("day_of_week").IsRequired();
        e.Property(x => x.ValidFrom).HasColumnName("valid_from").IsRequired();
        e.Property(x => x.ValidTo).HasColumnName("valid_to").IsRequired();

        e.HasOne(x => x.Class)
            .WithMany(c => c.Schedules)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.LecturerProfile)
            .WithMany(l => l.ClassSchedules)
            .HasForeignKey(x => x.LecturerCode)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Room)
            .WithMany(r => r.Schedules)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.TimeSlot)
            .WithMany(t => t.Schedules)
            .HasForeignKey(x => x.TimeSlotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ClassSessionConfiguration : IEntityTypeConfiguration<ClassSession>
{
    public void Configure(EntityTypeBuilder<ClassSession> e)
    {
        e.ToTable("class_sessions");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.ClassId).HasColumnName("class_id").IsRequired();
        e.Property(x => x.ScheduleId).HasColumnName("schedule_id");
        e.Property(x => x.LearnDate).HasColumnName("learn_date").IsRequired();
        e.Property(x => x.TimeSlotId).HasColumnName("time_slot_id").IsRequired();
        e.Property(x => x.RoomId).HasColumnName("room_id").IsRequired();
        e.Property(x => x.LecturerCode).HasColumnName("lecturer_code").HasMaxLength(20).IsRequired();
        e.Property(x => x.IsCancelled).HasColumnName("is_cancelled").HasDefaultValue(false);
        e.Property(x => x.Note).HasColumnName("note");

        e.HasOne(x => x.Class)
            .WithMany(c => c.Sessions)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Schedule)
            .WithMany(s => s.Sessions)
            .HasForeignKey(x => x.ScheduleId)
            .OnDelete(DeleteBehavior.SetNull);

        e.HasOne(x => x.TimeSlot)
            .WithMany(t => t.Sessions)
            .HasForeignKey(x => x.TimeSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Room)
            .WithMany(r => r.Sessions)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.LecturerProfile)
            .WithMany(l => l.ClassSessions)
            .HasForeignKey(x => x.LecturerCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> e)
    {
        e.ToTable("attendance");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SessionId).HasColumnName("session_id").IsRequired();
        e.Property(x => x.StudentCode).HasColumnName("student_code").HasMaxLength(20).IsRequired();
        e.Property(x => x.Status).HasColumnName("status").IsRequired();
        e.Property(x => x.RecordedAt).HasColumnName("recorded_at");
        e.Property(x => x.Comments).HasColumnName("comments");

        e.HasOne(x => x.Session)
            .WithMany(s => s.Attendances)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.StudentProfile)
            .WithMany(p => p.Attendances)
            .HasForeignKey(x => x.StudentCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

