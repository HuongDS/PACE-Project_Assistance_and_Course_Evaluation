using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicProposalFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProposalContentType",
                table: "group_topic_submissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ProposalFile",
                table: "group_topic_submissions",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposalFileName",
                table: "group_topic_submissions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProposalFileSize",
                table: "group_topic_submissions",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProposalContentType",
                table: "group_topic_submissions");

            migrationBuilder.DropColumn(
                name: "ProposalFile",
                table: "group_topic_submissions");

            migrationBuilder.DropColumn(
                name: "ProposalFileName",
                table: "group_topic_submissions");

            migrationBuilder.DropColumn(
                name: "ProposalFileSize",
                table: "group_topic_submissions");
        }
    }
}
