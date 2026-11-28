using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class GroupTopicSubmissionConfiguration : IEntityTypeConfiguration<GroupTopicSubmission>
{
    public void Configure(EntityTypeBuilder<GroupTopicSubmission> entity)
    {
        // Bảng này đặt tên cột theo PascalCase từ migration đầu, giữ nguyên để không phải đổi tên cột.
        entity.ToTable("group_topic_submissions");
        entity.HasKey(x => new { x.GroupId, x.Version });
        entity.Property(x => x.Title).HasMaxLength(255);
        entity.Property(x => x.Description).HasMaxLength(4000);
        entity.Property(x => x.Notes).HasMaxLength(4000);
        entity.Property(x => x.ProposalFileName).HasMaxLength(255);
        entity.Property(x => x.ProposalContentType).HasMaxLength(100);
        entity.Property(x => x.ProposalFile).HasColumnType("bytea");
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsConcurrencyToken();
        entity.Property(x => x.Summary).HasMaxLength(300);
        entity.Property(x => x.ContentJson).HasColumnType("jsonb");
        entity.Property(x => x.ContentHtml).HasColumnType("text");
        entity.Property(x => x.ProposalSource).HasConversion<string>().HasMaxLength(16);
        entity.Property(x => x.NoteToLecturer).HasMaxLength(1000);
        entity.HasIndex(x => x.TemplateId);
        entity.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId);
        // Xoá khung thì bản nộp vẫn còn: nội dung đã tự chứa tiêu đề các mục.
        entity.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ProposalTemplateConfiguration : IEntityTypeConfiguration<ProposalTemplate>
{
    public void Configure(EntityTypeBuilder<ProposalTemplate> entity)
    {
        entity.ToTable("proposal_templates");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(x => x.CourseId).HasColumnName("course_id");
        entity.Property(x => x.ClassId).HasColumnName("class_id");
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
        entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        entity.Property(x => x.SectionsJson).HasColumnName("sections").HasColumnType("jsonb");
        entity.Property(x => x.Version).HasColumnName("version");
        entity.Property(x => x.IsActive).HasColumnName("is_active");
        entity.Property(x => x.SortOrder).HasColumnName("sort_order");
        entity.Property(x => x.CreatedBy).HasColumnName("created_by");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.HasIndex(x => x.CourseId);
        entity.HasIndex(x => x.ClassId);
        entity.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class GroupTopicDraftConfiguration : IEntityTypeConfiguration<GroupTopicDraft>
{
    public void Configure(EntityTypeBuilder<GroupTopicDraft> entity)
    {
        entity.ToTable("group_topic_drafts");
        entity.HasKey(x => x.GroupId);
        entity.Property(x => x.GroupId).HasColumnName("group_id");
        entity.Property(x => x.TemplateId).HasColumnName("template_id");
        entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(255);
        entity.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(300);
        entity.Property(x => x.ContentJson).HasColumnName("content").HasColumnType("jsonb");
        entity.Property(x => x.NoteToLecturer).HasColumnName("note_to_lecturer").HasMaxLength(1000);
        entity.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        entity.Property(x => x.YDocState).HasColumnName("ydoc_state").HasColumnType("bytea");
        entity.Property(x => x.YDocSeq).HasColumnName("ydoc_seq");
        entity.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.HasIndex(x => x.TemplateId);
        entity.HasOne(x => x.Group).WithOne().HasForeignKey<GroupTopicDraft>(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class GroupTopicAttachmentConfiguration : IEntityTypeConfiguration<GroupTopicAttachment>
{
    public void Configure(EntityTypeBuilder<GroupTopicAttachment> entity)
    {
        entity.ToTable("group_topic_attachments");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(x => x.GroupId).HasColumnName("group_id");
        entity.Property(x => x.Version).HasColumnName("version");
        entity.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255);
        entity.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(127);
        entity.Property(x => x.FileSize).HasColumnName("file_size");
        entity.Property(x => x.Content).HasColumnName("content").HasColumnType("bytea");
        entity.Property(x => x.UploadedBy).HasColumnName("uploaded_by");
        entity.Property(x => x.UploadedAt).HasColumnName("uploaded_at");
        entity.HasIndex(x => new { x.GroupId, x.Version });
        entity.HasOne(x => x.Submission).WithMany(x => x.Attachments)
            .HasForeignKey(x => new { x.GroupId, x.Version }).OnDelete(DeleteBehavior.Cascade)
            // Tên mặc định dài quá 63 ký tự (giới hạn của PostgreSQL) nên đặt tên ngắn, khớp với TopicProposalSchema.
            .HasConstraintName("FK_group_topic_attachments_submissions");
    }
}

public class GroupTopicDraftUpdateConfiguration : IEntityTypeConfiguration<GroupTopicDraftUpdate>
{
    public void Configure(EntityTypeBuilder<GroupTopicDraftUpdate> entity)
    {
        entity.ToTable("group_topic_draft_updates");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        entity.Property(x => x.GroupId).HasColumnName("group_id");
        entity.Property(x => x.Update).HasColumnName("update").HasColumnType("bytea");
        entity.Property(x => x.UserId).HasColumnName("user_id");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        // Đọc theo nhóm và theo thứ tự: "cho tôi các thay đổi của nhóm X sau id N".
        entity.HasIndex(x => new { x.GroupId, x.Id });
        entity.HasOne(x => x.Draft).WithMany(x => x.Updates).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
