using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MenoScreen.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PortalSetupCodeExpiresAt",
                table: "Patients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortalSetupCodeHash",
                table: "Patients",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PortalSetupFailedAttempts",
                table: "Patients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WeeklyCheckIns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VasomotorSleep = table.Column<int>(type: "integer", nullable: false),
                    CognitiveMood = table.Column<int>(type: "integer", nullable: false),
                    MusculoskeletalEnergy = table.Column<int>(type: "integer", nullable: false),
                    UrogenitalSexual = table.Column<int>(type: "integer", nullable: false),
                    QualityOfLife = table.Column<int>(type: "integer", nullable: false),
                    HrtStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HrtStartedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WantsContact = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyCheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyCheckIns_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_UserId",
                table: "Patients",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyCheckIns_PatientId_CheckedInAt",
                table: "WeeklyCheckIns",
                columns: new[] { "PatientId", "CheckedInAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeeklyCheckIns");

            migrationBuilder.DropIndex(
                name: "IX_Patients_UserId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PortalSetupCodeExpiresAt",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PortalSetupCodeHash",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PortalSetupFailedAttempts",
                table: "Patients");
        }
    }
}
