using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzCert.Processor.Migrations
{
    /// <inheritdoc />
    public partial class AttemptSnapshotMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "AttemptQuestions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Topic",
                table: "AttemptQuestions");
        }
    }
}
