using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzCert.Processor.Migrations
{
    /// <inheritdoc />
    public partial class AttemptMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "Attempts",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mode",
                table: "Attempts");
        }
    }
}
