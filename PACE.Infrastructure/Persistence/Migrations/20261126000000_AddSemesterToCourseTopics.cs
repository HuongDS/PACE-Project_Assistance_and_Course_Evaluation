using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <summary>Đề tài mẫu mở theo kỳ. Viết bằng SQL chạy lại được, vì API cũng tự thêm cột lúc khởi động.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261126000000_AddSemesterToCourseTopics")]
    public partial class AddSemesterToCourseTopics : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CourseTopicSemesterSchema.Up);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_course_topics_semesters_semester_id", table: "course_topics");
            migrationBuilder.DropIndex(name: "IX_course_topics_semester_id", table: "course_topics");
            migrationBuilder.DropColumn(name: "semester_id", table: "course_topics");
        }
    }
}
