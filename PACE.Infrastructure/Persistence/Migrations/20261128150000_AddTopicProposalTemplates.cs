using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Đề xuất đề tài theo khung: khung đề xuất (kèm 4 khung mặc định), bản nháp tự lưu, tài liệu kèm theo,
    /// và nội dung rich text + nguồn file proposal trên group_topic_submissions.
    /// Viết bằng SQL chạy lại được, vì API cũng tự tạo bảng lúc khởi động.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261128150000_AddTopicProposalTemplates")]
    public partial class AddTopicProposalTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(TopicProposalSchema.Up);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(TopicProposalSchema.Down);
        }
    }
}
