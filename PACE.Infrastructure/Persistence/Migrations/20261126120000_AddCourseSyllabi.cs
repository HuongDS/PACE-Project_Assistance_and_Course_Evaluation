using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <summary>Syllabus của môn học. Viết bằng SQL chạy lại được, vì API cũng tự tạo bảng lúc khởi động.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261126120000_AddCourseSyllabi")]
    public partial class AddCourseSyllabi : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CourseSyllabusSchema.Up);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "course_syllabi");
        }
    }
}
