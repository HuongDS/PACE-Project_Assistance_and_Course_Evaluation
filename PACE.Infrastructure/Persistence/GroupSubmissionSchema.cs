namespace PACE.Infrastructure.Persistence;

/// <summary>SQL tạo bảng bài nộp của nhóm, dùng chung cho migration và bước khởi động. Chạy lại không đổi gì.</summary>
public static class GroupSubmissionSchema
{
    public const string Up = @"
                CREATE TABLE IF NOT EXISTS group_submissions (
                    id uuid NOT NULL,
                    group_id uuid NOT NULL,
                    milestone_id uuid,
                    deliverable character varying(255) NOT NULL,
                    file_name character varying(255),
                    content_type character varying(127),
                    file_size bigint,
                    content bytea,
                    url character varying(2000),
                    note character varying(1000),
                    submitted_by uuid NOT NULL,
                    submitted_at timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_group_submissions"" PRIMARY KEY (id),
                    CONSTRAINT ""FK_group_submissions_groups_group_id""
                        FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE CASCADE,
                    CONSTRAINT ""FK_group_submissions_class_milestones_milestone_id""
                        FOREIGN KEY (milestone_id) REFERENCES class_milestones(""Id"") ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS ""IX_group_submissions_group_id_milestone_id"" ON group_submissions(group_id, milestone_id);
                CREATE INDEX IF NOT EXISTS ""IX_group_submissions_milestone_id"" ON group_submissions(milestone_id);

                CREATE TABLE IF NOT EXISTS group_submission_reviews (
                    group_id uuid NOT NULL,
                    milestone_id uuid NOT NULL,
                    status character varying(20) NOT NULL,
                    score numeric(4,2),
                    feedback character varying(4000),
                    reviewed_by uuid NOT NULL,
                    reviewed_at timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_group_submission_reviews"" PRIMARY KEY (group_id, milestone_id),
                    CONSTRAINT ""FK_group_submission_reviews_groups_group_id""
                        FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE CASCADE,
                    CONSTRAINT ""FK_group_submission_reviews_class_milestones_milestone_id""
                        FOREIGN KEY (milestone_id) REFERENCES class_milestones(""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_group_submission_reviews_milestone_id"" ON group_submission_reviews(milestone_id);
            ";
}
