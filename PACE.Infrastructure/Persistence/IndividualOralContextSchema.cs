namespace PACE.Infrastructure.Persistence;

/// <summary>Bảng giữ mô hình nghiệp vụ tại mốc hạn của một phiên vấn đáp. Chỉ thêm, chạy lại được.</summary>
public static class IndividualOralContextSchema
{
    public const string Up = @"
                CREATE TABLE IF NOT EXISTS individual_oral_contexts (
                    session_id uuid NOT NULL,
                    context_json text NOT NULL,
                    CONSTRAINT ""PK_individual_oral_contexts"" PRIMARY KEY (session_id),
                    CONSTRAINT ""FK_individual_oral_contexts_individual_oral_setups_session_id"" FOREIGN KEY (session_id)
                        REFERENCES individual_oral_setups (session_id) ON DELETE CASCADE
                );";
}
