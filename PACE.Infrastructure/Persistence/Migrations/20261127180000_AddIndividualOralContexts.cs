using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <summary>Mô hình nghiệp vụ cho vấn đáp. Viết bằng SQL chạy lại được, vì API cũng tự tạo bảng lúc khởi động.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261127180000_AddIndividualOralContexts")]
    public partial class AddIndividualOralContexts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(IndividualOralContextSchema.Up);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "individual_oral_contexts");
        }
    }
}
