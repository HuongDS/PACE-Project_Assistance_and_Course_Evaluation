using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkSessionsToAssessmentRoundRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssessmentRoundRevisionId",
                table: "assessment_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_assessment_sessions_AssessmentRoundRevisionId",
                table: "assessment_sessions",
                column: "AssessmentRoundRevisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_assessment_sessions_assessment_round_revisions_AssessmentRo~",
                table: "assessment_sessions",
                column: "AssessmentRoundRevisionId",
                principalTable: "assessment_round_revisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_assessment_sessions_assessment_round_revisions_AssessmentRo~",
                table: "assessment_sessions");

            migrationBuilder.DropIndex(
                name: "IX_assessment_sessions_AssessmentRoundRevisionId",
                table: "assessment_sessions");

            migrationBuilder.DropColumn(
                name: "AssessmentRoundRevisionId",
                table: "assessment_sessions");
        }
    }
}
