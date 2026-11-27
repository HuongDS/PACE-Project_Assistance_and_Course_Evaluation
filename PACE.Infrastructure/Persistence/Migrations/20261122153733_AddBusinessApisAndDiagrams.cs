using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessApisAndDiagrams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_businesses_AnalysisRunId",
                table: "businesses");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "businesses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "businesses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderIndex",
                table: "businesses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OwnerStudentCode",
                table: "businesses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "businesses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateTable(
                name: "business_apis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_id = table.Column<Guid>(type: "uuid", nullable: false),
                    controller = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    function = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    http_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    order_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_apis", x => x.id);
                    table.ForeignKey(
                        name: "FK_business_apis_businesses_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "business_diagrams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_xml = table.Column<string>(type: "text", nullable: true),
                    specification_json = table.Column<string>(type: "jsonb", nullable: true),
                    preview_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_diagrams", x => x.id);
                    table.ForeignKey(
                        name: "FK_business_diagrams_businesses_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_businesses_AnalysisRunId_OrderIndex",
                table: "businesses",
                columns: new[] { "AnalysisRunId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_businesses_OwnerStudentCode",
                table: "businesses",
                column: "OwnerStudentCode");

            migrationBuilder.CreateIndex(
                name: "IX_business_apis_business_id_controller_function",
                table: "business_apis",
                columns: new[] { "business_id", "controller", "function" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_diagrams_business_id_title",
                table: "business_diagrams",
                columns: new[] { "business_id", "title" });

            migrationBuilder.AddForeignKey(
                name: "FK_businesses_student_profiles_OwnerStudentCode",
                table: "businesses",
                column: "OwnerStudentCode",
                principalTable: "student_profiles",
                principalColumn: "student_code",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_businesses_student_profiles_OwnerStudentCode",
                table: "businesses");

            migrationBuilder.DropTable(
                name: "business_apis");

            migrationBuilder.DropTable(
                name: "business_diagrams");

            migrationBuilder.DropIndex(
                name: "IX_businesses_AnalysisRunId_OrderIndex",
                table: "businesses");

            migrationBuilder.DropIndex(
                name: "IX_businesses_OwnerStudentCode",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "OrderIndex",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "OwnerStudentCode",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "businesses");

            migrationBuilder.CreateIndex(
                name: "IX_businesses_AnalysisRunId",
                table: "businesses",
                column: "AnalysisRunId");
        }
    }
}
