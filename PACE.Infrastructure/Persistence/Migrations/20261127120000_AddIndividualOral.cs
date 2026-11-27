using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <summary>Vấn đáp riêng từng sinh viên. Viết bằng SQL chạy lại được, vì API cũng tự tạo bảng lúc khởi động.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261127120000_AddIndividualOral")]
    public partial class AddIndividualOral : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(IndividualOralSchema.Up);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "oral_attempts");
            migrationBuilder.DropTable(name: "individual_oral_setups");
            migrationBuilder.DropColumn(name: "is_individual", table: "assessment_sessions");
        }
    }
}
