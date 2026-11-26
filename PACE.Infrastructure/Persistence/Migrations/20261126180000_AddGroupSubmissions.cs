using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <summary>Bài nộp và đánh giá bài nộp của nhóm. Viết bằng SQL chạy lại được, vì API cũng tự tạo bảng lúc khởi động.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261126180000_AddGroupSubmissions")]
    public partial class AddGroupSubmissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(GroupSubmissionSchema.Up);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "group_submission_reviews");
            migrationBuilder.DropTable(name: "group_submissions");
        }
    }
}
