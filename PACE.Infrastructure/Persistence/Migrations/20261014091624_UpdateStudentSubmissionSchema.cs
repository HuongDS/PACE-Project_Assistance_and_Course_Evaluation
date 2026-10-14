using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStudentSubmissionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_student_answers_UserId_QuestionId",
                table: "student_answers");

            migrationBuilder.DropColumn(
                name: "AiScore",
                table: "student_answers");

            migrationBuilder.AddColumn<string>(
                name: "StudentCode",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionId",
                table: "student_answers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "student_submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_student_submissions_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_submissions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_StudentCode",
                table: "users",
                column: "StudentCode",
                unique: true,
                filter: "\"StudentCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_answers_SubmissionId_QuestionId",
                table: "student_answers",
                columns: new[] { "SubmissionId", "QuestionId" },
                unique: true,
                filter: "\"SubmissionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_answers_UserId",
                table: "student_answers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_student_submissions_AnalysisRunId",
                table: "student_submissions",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_student_submissions_UserId",
                table: "student_submissions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_student_answers_student_submissions_SubmissionId",
                table: "student_answers",
                column: "SubmissionId",
                principalTable: "student_submissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Multiple submissions may now contain the same user's question.
            // Refuse rollback rather than delete answers to restore the old unique index.
            migrationBuilder.Sql("""
                DO $pace$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM student_answers
                        GROUP BY "UserId", "QuestionId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot roll back: duplicate UserId/QuestionId pairs exist. Back up and resolve them without losing answers first.';
                    END IF;
                END;
                $pace$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_student_answers_student_submissions_SubmissionId",
                table: "student_answers");

            migrationBuilder.DropTable(
                name: "student_submissions");

            migrationBuilder.DropIndex(
                name: "IX_users_StudentCode",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_student_answers_SubmissionId_QuestionId",
                table: "student_answers");

            migrationBuilder.DropIndex(
                name: "IX_student_answers_UserId",
                table: "student_answers");

            migrationBuilder.DropColumn(
                name: "StudentCode",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "student_answers");

            migrationBuilder.AddColumn<double>(
                name: "AiScore",
                table: "student_answers",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_answers_UserId_QuestionId",
                table: "student_answers",
                columns: new[] { "UserId", "QuestionId" },
                unique: true);
        }
    }
}
