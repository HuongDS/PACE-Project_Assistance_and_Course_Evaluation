namespace PACE.Infrastructure.Persistence;

/// <summary>
/// SQL cho đề xuất đề tài theo khung: bảng khung, bản nháp, tài liệu kèm theo và các cột mới của group_topic_submissions.
/// Dùng chung cho migration và bước khởi động. Chạy lại không đổi gì; khung mặc định chỉ thêm khi chưa có.
/// </summary>
public static class TopicProposalSchema
{
    public const string Up = @"
                CREATE TABLE IF NOT EXISTS proposal_templates (
                    id uuid NOT NULL,
                    course_id uuid,
                    class_id uuid,
                    name character varying(150) NOT NULL,
                    description character varying(500),
                    sections jsonb NOT NULL,
                    version integer NOT NULL,
                    is_active boolean NOT NULL,
                    sort_order integer NOT NULL,
                    created_by uuid,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_proposal_templates"" PRIMARY KEY (id),
                    CONSTRAINT ""FK_proposal_templates_courses_course_id""
                        FOREIGN KEY (course_id) REFERENCES courses(""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_proposal_templates_classes_class_id""
                        FOREIGN KEY (class_id) REFERENCES classes(id) ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_proposal_templates_course_id"" ON proposal_templates(course_id);
                CREATE INDEX IF NOT EXISTS ""IX_proposal_templates_class_id"" ON proposal_templates(class_id);

                ALTER TABLE group_topic_submissions ADD COLUMN IF NOT EXISTS ""Summary"" character varying(300);
                ALTER TABLE group_topic_submissions ADD COLUMN IF NOT EXISTS ""TemplateId"" uuid;
                ALTER TABLE group_topic_submissions ADD COLUMN IF NOT EXISTS ""TemplateVersion"" integer;
                ALTER TABLE group_topic_submissions ADD COLUMN IF NOT EXISTS ""ContentJson"" jsonb;
                ALTER TABLE group_topic_submissions ADD COLUMN IF NOT EXISTS ""ContentHtml"" text;
                ALTER TABLE group_topic_submissions ADD COLUMN IF NOT EXISTS ""ProposalSource"" character varying(16) NOT NULL DEFAULT 'Uploaded';
                ALTER TABLE group_topic_submissions ADD COLUMN IF NOT EXISTS ""NoteToLecturer"" character varying(1000);
                CREATE INDEX IF NOT EXISTS ""IX_group_topic_submissions_TemplateId"" ON group_topic_submissions(""TemplateId"");
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_group_topic_submissions_proposal_templates_TemplateId') THEN
                        ALTER TABLE group_topic_submissions ADD CONSTRAINT ""FK_group_topic_submissions_proposal_templates_TemplateId""
                            FOREIGN KEY (""TemplateId"") REFERENCES proposal_templates(id) ON DELETE SET NULL;
                    END IF;
                END $$;

                CREATE TABLE IF NOT EXISTS group_topic_drafts (
                    group_id uuid NOT NULL,
                    template_id uuid,
                    title character varying(255),
                    summary character varying(300),
                    content jsonb NOT NULL,
                    note_to_lecturer character varying(1000),
                    revision integer NOT NULL,
                    updated_by uuid NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_group_topic_drafts"" PRIMARY KEY (group_id),
                    CONSTRAINT ""FK_group_topic_drafts_groups_group_id""
                        FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE CASCADE,
                    CONSTRAINT ""FK_group_topic_drafts_proposal_templates_template_id""
                        FOREIGN KEY (template_id) REFERENCES proposal_templates(id) ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS ""IX_group_topic_drafts_template_id"" ON group_topic_drafts(template_id);

                CREATE TABLE IF NOT EXISTS group_topic_attachments (
                    id uuid NOT NULL,
                    group_id uuid NOT NULL,
                    version integer NOT NULL,
                    file_name character varying(255) NOT NULL,
                    content_type character varying(127),
                    file_size bigint NOT NULL,
                    content bytea NOT NULL,
                    uploaded_by uuid NOT NULL,
                    uploaded_at timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_group_topic_attachments"" PRIMARY KEY (id),
                    CONSTRAINT ""FK_group_topic_attachments_submissions""
                        FOREIGN KEY (group_id, version) REFERENCES group_topic_submissions(""GroupId"", ""Version"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_group_topic_attachments_group_id_version"" ON group_topic_attachments(group_id, version);

                INSERT INTO proposal_templates (id, course_id, class_id, name, description, sections, version, is_active, sort_order, created_by, created_at, updated_at)
                VALUES
                ('5e7a0c11-0000-4000-8000-000000000001', NULL, NULL, 'Ứng dụng web',
                 'Hệ thống quản lý, thương mại điện tử, nền tảng dịch vụ.', " + WebSections + @", 1, true, 10, NULL, now(), now()),
                ('5e7a0c11-0000-4000-8000-000000000002', NULL, NULL, 'Ứng dụng di động',
                 'App Android / iOS, có thể kèm trang quản trị web.', " + MobileSections + @", 1, true, 20, NULL, now(), now()),
                ('5e7a0c11-0000-4000-8000-000000000003', NULL, NULL, 'AI và phân tích dữ liệu',
                 'Đề tài có mô hình học máy, xử lý dữ liệu hoặc LLM.', " + AiSections + @", 1, true, 30, NULL, now(), now()),
                ('5e7a0c11-0000-4000-8000-000000000004', NULL, NULL, 'Tự trình bày',
                 'Một ô soạn thảo duy nhất, chỉ bắt buộc phần thành viên.', " + FreeSections + @", 1, true, 40, NULL, now(), now())
                ON CONFLICT (id) DO NOTHING;
            ";

    // Các mục dùng lại giữa các khung mặc định. Dựng JSON bằng jsonb_build_object để SQL không có dấu { },
    // vì ExecuteSqlRaw coi { } là chỗ điền tham số.
    private const string Context = "jsonb_build_object('key', 'context', 'title', 'Bối cảnh và bài toán', 'hint', 'Vấn đề thực tế nào đang tồn tại? Ai gặp vấn đề đó và hiện họ xử lý ra sao?', 'minWords', 60, 'required', true, 'type', 'richtext')";
    private const string Goal = "jsonb_build_object('key', 'goal', 'title', 'Mục tiêu đề tài', 'hint', '2–4 mục tiêu kiểm chứng được khi kết thúc môn học.', 'minWords', 30, 'required', true, 'type', 'richtext')";
    private const string Users = "jsonb_build_object('key', 'users', 'title', 'Đối tượng người dùng', 'hint', 'Các vai trò (actor) và nhu cầu chính của từng vai trò.', 'minWords', 30, 'required', true, 'type', 'richtext')";
    private const string Features = "jsonb_build_object('key', 'features', 'title', 'Phạm vi và chức năng chính', 'hint', 'Liệt kê chức năng theo vai trò. Ghi rõ phần nhóm KHÔNG làm để giảng viên đánh giá độ vừa sức.', 'minWords', 60, 'required', true, 'type', 'richtext')";
    private const string Platform = "jsonb_build_object('key', 'platform', 'title', 'Nền tảng và thiết bị', 'hint', 'Android, iOS hay cả hai; phiên bản tối thiểu; có cần chạy offline không.', 'minWords', 20, 'required', true, 'type', 'richtext')";
    private const string Data = "jsonb_build_object('key', 'data', 'title', 'Dữ liệu sử dụng', 'hint', 'Nguồn dữ liệu, quy mô, cách thu thập và vấn đề bản quyền / quyền riêng tư.', 'minWords', 40, 'required', true, 'type', 'richtext')";
    private const string Method = "jsonb_build_object('key', 'method', 'title', 'Phương pháp / mô hình', 'hint', 'Mô hình hoặc thuật toán dự kiến và lý do chọn.', 'minWords', 40, 'required', true, 'type', 'richtext')";
    private const string Eval = "jsonb_build_object('key', 'eval', 'title', 'Cách đánh giá kết quả', 'hint', 'Chỉ số đo (accuracy, thời gian phản hồi…) và ngưỡng nhóm đặt ra.', 'minWords', 20, 'required', true, 'type', 'richtext')";
    private const string Tech = "jsonb_build_object('key', 'tech', 'title', 'Công nghệ dự kiến', 'hint', 'Frontend, backend, CSDL, triển khai, kèm lý do chọn.', 'minWords', 20, 'required', true, 'type', 'richtext')";
    private const string Team = "jsonb_build_object('key', 'team', 'title', 'Thành viên và phân công', 'hint', 'Danh sách lấy từ nhóm. Ghi phần việc chính của từng người.', 'minWords', 0, 'required', true, 'type', 'team')";
    private const string Refs = "jsonb_build_object('key', 'refs', 'title', 'Tài liệu tham khảo', 'hint', 'Sản phẩm tương tự, bài báo, tài liệu kỹ thuật.', 'minWords', 0, 'required', false, 'type', 'richtext')";
    private const string Free = "jsonb_build_object('key', 'free', 'title', 'Nội dung đề xuất', 'hint', 'Trình bày tự do: bài toán, người dùng, chức năng chính, công nghệ.', 'minWords', 120, 'required', true, 'type', 'richtext')";

    private const string WebSections = "jsonb_build_array(" + Context + ", " + Users + ", " + Features + ", " + Tech + ", " + Team + ", " + Refs + ")";
    private const string MobileSections = "jsonb_build_array(" + Context + ", " + Users + ", " + Features + ", " + Platform + ", " + Tech + ", " + Team + ")";
    private const string AiSections = "jsonb_build_array(" + Context + ", " + Goal + ", " + Data + ", " + Method + ", " + Eval + ", " + Tech + ", " + Team + ")";
    private const string FreeSections = "jsonb_build_array(" + Free + ", " + Team + ")";

    public const string Down = @"
                DROP TABLE IF EXISTS group_topic_attachments;
                DROP TABLE IF EXISTS group_topic_drafts;
                ALTER TABLE group_topic_submissions DROP CONSTRAINT IF EXISTS ""FK_group_topic_submissions_proposal_templates_TemplateId"";
                DROP INDEX IF EXISTS ""IX_group_topic_submissions_TemplateId"";
                ALTER TABLE group_topic_submissions
                    DROP COLUMN IF EXISTS ""Summary"",
                    DROP COLUMN IF EXISTS ""TemplateId"",
                    DROP COLUMN IF EXISTS ""TemplateVersion"",
                    DROP COLUMN IF EXISTS ""ContentJson"",
                    DROP COLUMN IF EXISTS ""ContentHtml"",
                    DROP COLUMN IF EXISTS ""ProposalSource"",
                    DROP COLUMN IF EXISTS ""NoteToLecturer"";
                DROP TABLE IF EXISTS proposal_templates;
            ";
}
