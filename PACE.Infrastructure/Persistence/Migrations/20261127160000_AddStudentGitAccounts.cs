using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <summary>Tài khoản git của sinh viên. Viết bằng SQL chạy lại được, vì API cũng tự tạo bảng lúc khởi động.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261127160000_AddStudentGitAccounts")]
    public partial class AddStudentGitAccounts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(StudentGitAccountSchema.Up);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "student_git_accounts");
        }
    }
}
