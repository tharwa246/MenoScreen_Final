using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MenoScreen.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssistantPrompts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantPrompts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BandContents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Band = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClinicianMessage = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    ClinicianSummary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    PatientHandout = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BandContents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuidelineSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Heading = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ApprovedOn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidelineSections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BandContents_Band",
                table: "BandContents",
                column: "Band",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantPrompts");

            migrationBuilder.DropTable(
                name: "BandContents");

            migrationBuilder.DropTable(
                name: "GuidelineSections");
        }
    }
}
