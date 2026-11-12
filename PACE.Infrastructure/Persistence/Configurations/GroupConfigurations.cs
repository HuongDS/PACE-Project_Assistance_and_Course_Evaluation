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
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        e.HasOne(x => x.Class)
            .WithMany(c => c.Groups)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);
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

