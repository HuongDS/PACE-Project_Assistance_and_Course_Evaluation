using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentOralEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OralEvidenceJson",
                table: "session_students",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OralQuestionsJson",
                table: "assessment_sessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OralEvidenceJson",
                table: "session_students");

            migrationBuilder.DropColumn(
                name: "OralQuestionsJson",
                table: "assessment_sessions");
        }
    }
}
