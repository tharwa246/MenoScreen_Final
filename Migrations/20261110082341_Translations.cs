using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MenoScreen.Migrations
{
    /// <inheritdoc />
    public partial class Translations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReviewed",
                table: "QuestionTranslations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "QuestionTranslations",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "QuestionTranslations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReviewed",
                table: "QuestionTranslations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "QuestionTranslations");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "QuestionTranslations");
        }
    }
}
