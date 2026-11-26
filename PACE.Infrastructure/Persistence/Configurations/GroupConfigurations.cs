using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> e)
    {
        e.ToTable("groups");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.ClassId).HasColumnName("class_id").IsRequired();
        e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        e.Property(x => x.TopicName).HasColumnName("topic_name").HasMaxLength(255);
        e.Property(x => x.CourseTopicId).HasColumnName("course_topic_id");
        e.Property(x => x.IsLocked).HasColumnName("is_locked").HasDefaultValue(false);
        e.Property(x => x.IsApproved).HasColumnName("is_approved").HasDefaultValue(true);
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        e.HasOne(x => x.Class)
            .WithMany(c => c.Groups)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.CourseTopic)
            .WithMany(t => t.Groups)
            .HasForeignKey(x => x.CourseTopicId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class CourseTopicConfiguration : IEntityTypeConfiguration<CourseTopic>
{
    public void Configure(EntityTypeBuilder<CourseTopic> e)
    {
        e.ToTable("course_topics");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.CourseId).HasColumnName("course_id").IsRequired();
        e.Property(x => x.SemesterId).HasColumnName("semester_id");
        e.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        e.Property(x => x.Description).HasColumnName("description");
        e.Property(x => x.Requirements).HasColumnName("requirements");
        e.Property(x => x.MaxGroups).HasColumnName("max_groups").HasDefaultValue(1);
        e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        e.HasOne(x => x.Course)
            .WithMany(c => c.Topics)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Semester)
            .WithMany()
            .HasForeignKey(x => x.SemesterId)
            .OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => x.CourseId);
        e.HasIndex(x => x.SemesterId);
    }
}

public class GroupInvitationConfiguration : IEntityTypeConfiguration<GroupInvitation>
{
    public void Configure(EntityTypeBuilder<GroupInvitation> e)
    {
        e.ToTable("group_invitations");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.GroupId).HasColumnName("group_id").IsRequired();
        e.Property(x => x.InvitedStudentCode).HasColumnName("invited_student_code").HasMaxLength(20).IsRequired();
        e.Property(x => x.InvitedByUserId).HasColumnName("invited_by_user_id").IsRequired();
        e.Property(x => x.Status).HasColumnName("status").HasConversion<int>().HasDefaultValue(PACE.Domain.Enums.GroupInvitationStatus.Pending);
        e.Property(x => x.Message).HasColumnName("message").HasMaxLength(500);
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        e.Property(x => x.RespondedAt).HasColumnName("responded_at");

        e.HasOne(x => x.Group)
            .WithMany(g => g.Invitations)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.InvitedStudent)
            .WithMany(s => s.GroupInvitations)
            .HasForeignKey(x => x.InvitedStudentCode)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.InvitedByUser)
            .WithMany()
            .HasForeignKey(x => x.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.GroupId, x.InvitedStudentCode, x.Status });
    }
}

public class GroupChangeRequestConfiguration : IEntityTypeConfiguration<GroupChangeRequest>
{
    public void Configure(EntityTypeBuilder<GroupChangeRequest> e)
    {
        e.ToTable("group_change_requests");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.GroupId).HasColumnName("group_id").IsRequired();
        e.Property(x => x.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        e.Property(x => x.Status).HasColumnName("status").HasConversion<int>().HasDefaultValue(PACE.Domain.Enums.GroupChangeRequestStatus.Pending);
        e.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        e.Property(x => x.Reason).HasColumnName("reason").IsRequired();
        e.Property(x => x.PayloadJson).HasColumnName("payload_json").HasDefaultValue("{}");
        e.Property(x => x.FailStudentIfRemoved).HasColumnName("fail_student_if_removed").HasDefaultValue(false);
        e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        e.Property(x => x.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        e.Property(x => x.ReviewNotes).HasColumnName("review_notes");
        e.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");

        e.HasOne(x => x.Group)
            .WithMany(g => g.ChangeRequests)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.ReviewedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.GroupId, x.Status });
    }
}

public class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> e)
    {
        e.ToTable("group_members");
        e.HasKey(x => new { x.GroupId, x.StudentCode });

        e.Property(x => x.GroupId).HasColumnName("group_id");
        e.Property(x => x.StudentCode).HasColumnName("student_code").HasMaxLength(20);
        e.Property(x => x.Role).HasColumnName("role").HasMaxLength(50).HasDefaultValue("Member");
        e.Property(x => x.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("now()");

        e.HasOne(x => x.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.StudentProfile)
            .WithMany(s => s.GroupMembers)
            .HasForeignKey(x => x.StudentCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

