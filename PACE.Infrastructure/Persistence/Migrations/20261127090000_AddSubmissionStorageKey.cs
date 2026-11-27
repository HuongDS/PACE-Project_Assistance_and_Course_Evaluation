using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <summary>Khóa của tệp nộp ở kho ngoài. Bước khởi động của API cũng thêm cột này, nên SQL chạy lại được.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261127090000_AddSubmissionStorageKey")]
    public partial class AddSubmissionStorageKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(GroupSubmissionSchema.Up);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "storage_key", table: "group_submissions");
        }
    }
}
