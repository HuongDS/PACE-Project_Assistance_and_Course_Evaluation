using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisRunBusinessTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessTemplateJson",
                table: "analysis_runs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateSource",
                table: "analysis_runs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TemplateUpdatedAt",
                table: "analysis_runs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessTemplateJson",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "TemplateSource",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "TemplateUpdatedAt",
                table: "analysis_runs");
        }
    }
}
