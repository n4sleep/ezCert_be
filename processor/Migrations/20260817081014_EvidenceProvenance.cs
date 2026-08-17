using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzCert.Processor.Migrations
{
    /// <inheritdoc />
    public partial class EvidenceProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Section",
                table: "Questions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "Questions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Section",
                table: "QuestionCitations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceTitle",
                table: "QuestionCitations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Section",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Topic",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Section",
                table: "QuestionCitations");

            migrationBuilder.DropColumn(
                name: "SourceTitle",
                table: "QuestionCitations");
        }
    }
}
