using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceCodeAndDataFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContextType",
                table: "generated_questions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Raw");

            migrationBuilder.CreateTable(
                name: "method_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClassName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MethodName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Parameters = table.Column<string>(type: "text", nullable: true),
                    SourceCode = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NodeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_method_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_method_sources_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "business_flows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryMethodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MermaidGraph = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_flows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_flows_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_business_flows_method_sources_EntryMethodId",
                        column: x => x.EntryMethodId,
                        principalTable: "method_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "business_methods",
                columns: table => new
                {
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    MethodSourceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_methods", x => new { x.BusinessId, x.MethodSourceId });
                    table.ForeignKey(
                        name: "FK_business_methods_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_business_methods_method_sources_MethodSourceId",
                        column: x => x.MethodSourceId,
                        principalTable: "method_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "data_flow_edges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    FromMethodId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToMethodId = table.Column<Guid>(type: "uuid", nullable: false),
                    EdgeType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ConditionContext = table.Column<string>(type: "text", nullable: true),
                    DataLabel = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_flow_edges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_data_flow_edges_business_flows_FlowId",
                        column: x => x.FlowId,
                        principalTable: "business_flows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_data_flow_edges_method_sources_FromMethodId",
                        column: x => x.FromMethodId,
                        principalTable: "method_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_data_flow_edges_method_sources_ToMethodId",
                        column: x => x.ToMethodId,
                        principalTable: "method_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_flows_BusinessId",
                table: "business_flows",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_business_flows_EntryMethodId",
                table: "business_flows",
                column: "EntryMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_business_methods_MethodSourceId",
                table: "business_methods",
                column: "MethodSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_data_flow_edges_FlowId_StepOrder",
                table: "data_flow_edges",
                columns: new[] { "FlowId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_flow_edges_FromMethodId",
                table: "data_flow_edges",
                column: "FromMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_data_flow_edges_ToMethodId",
                table: "data_flow_edges",
                column: "ToMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_method_sources_AnalysisRunId_ClassName_MethodName",
                table: "method_sources",
                columns: new[] { "AnalysisRunId", "ClassName", "MethodName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_methods");

            migrationBuilder.DropTable(
                name: "data_flow_edges");

            migrationBuilder.DropTable(
                name: "business_flows");

            migrationBuilder.DropTable(
                name: "method_sources");

            migrationBuilder.DropColumn(
                name: "ContextType",
                table: "generated_questions");
        }
    }
}
