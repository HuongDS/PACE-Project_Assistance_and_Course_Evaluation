using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropRubricTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rubric_scores");

            migrationBuilder.DropTable(
                name: "rubric_criteria");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rubric_criteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Statement = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubric_criteria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rubric_scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CriterionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentAnswerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubric_scores", x => x.Id);
                    table.CheckConstraint("CK_rubric_scores_Score", "\"Score\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_rubric_scores_rubric_criteria_CriterionId",
                        column: x => x.CriterionId,
                        principalTable: "rubric_criteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rubric_scores_student_answers_StudentAnswerId",
                        column: x => x.StudentAnswerId,
                        principalTable: "student_answers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "rubric_criteria",
                columns: new[] { "Id", "Code", "DisplayOrder", "Name", "Statement" },
                values: new object[,]
                {
                    { new Guid("b2e1d3c5-0001-4d7f-8c2b-000000000001"), "ACCURACY", 1, "Độ chính xác kỹ thuật", "Nhận xét đúng về mặt kỹ thuật so với code thực tế trong repo của nhóm tôi." },
                    { new Guid("b2e1d3c5-0001-4d7f-8c2b-000000000002"), "FAIRNESS", 2, "Công bằng", "Điểm số và nhận xét phản ánh công bằng chất lượng câu trả lời của tôi." },
                    { new Guid("b2e1d3c5-0001-4d7f-8c2b-000000000003"), "SPECIFICITY", 3, "Cụ thể", "Nhận xét chỉ rõ phần nào trong câu trả lời của tôi đúng, phần nào sai hoặc còn thiếu." },
                    { new Guid("b2e1d3c5-0001-4d7f-8c2b-000000000004"), "ACTIONABILITY", 4, "Có thể hành động", "Sau khi đọc, tôi biết cụ thể cần làm gì để cải thiện câu trả lời." },
                    { new Guid("b2e1d3c5-0001-4d7f-8c2b-000000000005"), "USEFULNESS", 5, "Hữu ích", "Nhận xét này giúp tôi hiểu rõ hơn luồng xử lý trong project của mình." },
                    { new Guid("b2e1d3c5-0001-4d7f-8c2b-000000000006"), "CLARITY", 6, "Rõ ràng", "Nhận xét được trình bày rõ ràng, dễ hiểu." },
                    { new Guid("b2e1d3c5-0001-4d7f-8c2b-000000000007"), "ACCEPTANCE", 7, "Chấp nhận", "Tôi đồng ý và chấp nhận nhận xét này." },
                    { new Guid("b2e1d3c5-0001-4d7f-8c2b-000000000008"), "WILLINGNESS", 8, "Sẵn sàng cải thiện", "Tôi sẵn sàng sửa lại câu trả lời dựa trên nhận xét này." },
                    { new Guid("b2e1d3c5-0001-4d7f-8c2b-000000000009"), "AFFECT", 9, "Cảm xúc", "Đọc nhận xét này khiến tôi thấy được động viên, không bị nản." }
                });

            migrationBuilder.CreateIndex(
                name: "IX_rubric_criteria_Code",
                table: "rubric_criteria",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rubric_scores_CriterionId",
                table: "rubric_scores",
                column: "CriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_rubric_scores_StudentAnswerId_CriterionId",
                table: "rubric_scores",
                columns: new[] { "StudentAnswerId", "CriterionId" },
                unique: true);
        }
    }
}
