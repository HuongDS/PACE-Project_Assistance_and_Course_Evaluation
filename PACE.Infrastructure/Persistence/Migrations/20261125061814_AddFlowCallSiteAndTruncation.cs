using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFlowCallSiteAndTruncation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CallSiteColumn",
                table: "data_flow_edges",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CallSiteLine",
                table: "data_flow_edges",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TruncationReason",
                table: "business_flows",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CallSiteColumn",
                table: "data_flow_edges");

            migrationBuilder.DropColumn(
                name: "CallSiteLine",
                table: "data_flow_edges");

            migrationBuilder.DropColumn(
                name: "TruncationReason",
                table: "business_flows");
        }
    }
}
