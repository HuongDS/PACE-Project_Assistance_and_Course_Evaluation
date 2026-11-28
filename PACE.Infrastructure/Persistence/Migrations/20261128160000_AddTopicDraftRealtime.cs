using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Soạn bản nháp đề xuất cùng lúc: trạng thái Yjs đã gộp trên group_topic_drafts và log thay đổi group_topic_draft_updates.
    /// Viết bằng SQL chạy lại được, vì API cũng tự tạo bảng lúc khởi động.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261128160000_AddTopicDraftRealtime")]
    public partial class AddTopicDraftRealtime : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(TopicDraftRealtimeSchema.Up);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(TopicDraftRealtimeSchema.Down);
        }
    }
}
