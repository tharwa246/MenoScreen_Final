using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MenoScreen.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultationTiming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsultationDurationSeconds",
                table: "Screenings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsultationEndedAt",
                table: "Screenings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsultationStartedAt",
                table: "Screenings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsultationDurationSeconds",
                table: "Screenings");

            migrationBuilder.DropColumn(
                name: "ConsultationEndedAt",
                table: "Screenings");

            migrationBuilder.DropColumn(
                name: "ConsultationStartedAt",
                table: "Screenings");
        }
    }
}
