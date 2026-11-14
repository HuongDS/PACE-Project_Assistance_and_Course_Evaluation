using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOralExamToAssessmentSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_groups_analysis_runs_analysis_run_id",
                table: "groups");

            migrationBuilder.DropIndex(
                name: "IX_groups_analysis_run_id",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "analysis_run_id",
                table: "groups");

            migrationBuilder.AddColumn<Guid>(
                name: "oral_exam_id",
                table: "assessment_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "analysis_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_assessment_sessions_oral_exam_id",
                table: "assessment_sessions",
                column: "oral_exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_runs_group_id",
                table: "analysis_runs",
                column: "group_id");

            migrationBuilder.AddForeignKey(
                name: "FK_analysis_runs_groups_group_id",
                table: "analysis_runs",
                column: "group_id",
                principalTable: "groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_assessment_sessions_oral_exams_oral_exam_id",
                table: "assessment_sessions",
                column: "oral_exam_id",
                principalTable: "oral_exams",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_analysis_runs_groups_group_id",
                table: "analysis_runs");

            migrationBuilder.DropForeignKey(
                name: "FK_assessment_sessions_oral_exams_oral_exam_id",
                table: "assessment_sessions");

            migrationBuilder.DropIndex(
                name: "IX_assessment_sessions_oral_exam_id",
                table: "assessment_sessions");

            migrationBuilder.DropIndex(
                name: "IX_analysis_runs_group_id",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "oral_exam_id",
                table: "assessment_sessions");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "analysis_runs");

            migrationBuilder.AddColumn<Guid>(
                name: "analysis_run_id",
                table: "groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_groups_analysis_run_id",
                table: "groups",
                column: "analysis_run_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_groups_analysis_runs_analysis_run_id",
                table: "groups",
                column: "analysis_run_id",
                principalTable: "analysis_runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
