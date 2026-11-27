namespace PACE.Infrastructure.Persistence;

/// <summary>Bảng cho vấn đáp riêng từng sinh viên. Chỉ thêm, chạy lại được: dùng chung cho migration và lúc khởi động.</summary>
public static class IndividualOralSchema
{
    public const string Up = @"
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS is_individual boolean NOT NULL DEFAULT FALSE;
                CREATE TABLE IF NOT EXISTS individual_oral_setups (
                    session_id uuid NOT NULL,
                    duration_minutes integer NOT NULL,
                    questions_per_student integer NOT NULL,
                    difficulty character varying(20) NOT NULL,
                    code_deadline timestamp with time zone,
                    commit_sha character varying(64),
                    commit_at timestamp with time zone,
                    scanned_at timestamp with time zone,
                    snapshot_json text,
                    CONSTRAINT ""PK_individual_oral_setups"" PRIMARY KEY (session_id),
                    CONSTRAINT ""FK_individual_oral_setups_assessment_sessions_session_id"" FOREIGN KEY (session_id)
                        REFERENCES assessment_sessions (""Id"") ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS oral_attempts (
                    session_id uuid NOT NULL,
                    student_id uuid NOT NULL,
                    questions_json text NOT NULL,
                    confirmed_at timestamp with time zone,
                    started_at timestamp with time zone,
                    expires_at timestamp with time zone,
                    submitted_at timestamp with time zone,
                    answers_json text NOT NULL,
                    ai_review_json text,
                    ai_reviewed_at timestamp with time zone,
                    history_json text,
                    version uuid NOT NULL,
                    CONSTRAINT ""PK_oral_attempts"" PRIMARY KEY (session_id, student_id),
                    CONSTRAINT ""FK_oral_attempts_session_students_session_id_student_id"" FOREIGN KEY (session_id, student_id)
                        REFERENCES session_students (""SessionId"", ""StudentId"") ON DELETE CASCADE
                );";
}
