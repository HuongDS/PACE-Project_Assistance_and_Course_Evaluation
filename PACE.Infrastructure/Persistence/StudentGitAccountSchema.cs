namespace PACE.Infrastructure.Persistence;

/// <summary>Bảng tài khoản git của sinh viên. Chỉ thêm, chạy lại được: dùng chung cho migration và lúc khởi động.</summary>
public static class StudentGitAccountSchema
{
    public const string Up = @"
                CREATE TABLE IF NOT EXISTS student_git_accounts (
                    user_id uuid NOT NULL,
                    github_username character varying(39),
                    emails character varying(1300),
                    updated_at timestamp with time zone NOT NULL,
                    updated_by uuid NOT NULL,
                    CONSTRAINT ""PK_student_git_accounts"" PRIMARY KEY (user_id),
                    CONSTRAINT ""FK_student_git_accounts_users_user_id"" FOREIGN KEY (user_id)
                        REFERENCES users (""Id"") ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_student_git_accounts_github_username""
                    ON student_git_accounts (github_username);";
}
