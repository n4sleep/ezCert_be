using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EzCert.Processor.Migrations
{
    /// <inheritdoc />
    public partial class JobWorkerStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "ProcessingJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "ProcessingJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "ProcessingJobs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "ProcessingJobs");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "ProcessingJobs");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "ProcessingJobs");
        }
    }
}
