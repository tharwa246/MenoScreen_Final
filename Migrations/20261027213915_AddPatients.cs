using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MenoScreen.Migrations
{
    /// <inheritdoc />
    public partial class AddPatients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClinicianId",
                table: "Screenings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PatientId",
                table: "Screenings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PreferredLanguage = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Screenings_PatientId",
                table: "Screenings",
                column: "PatientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Screenings_Patients_PatientId",
                table: "Screenings",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Screenings_Patients_PatientId",
                table: "Screenings");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Screenings_PatientId",
                table: "Screenings");

            migrationBuilder.DropColumn(
                name: "ClinicianId",
                table: "Screenings");

            migrationBuilder.DropColumn(
                name: "PatientId",
                table: "Screenings");
        }
    }
}
