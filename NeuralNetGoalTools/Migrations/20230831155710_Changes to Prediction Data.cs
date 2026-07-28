using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeuralNetGoalTools.Migrations
{
    /// <inheritdoc />
    public partial class ChangestoPredictionData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BOOK1",
                table: "predictionsData",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BOOK2",
                table: "predictionsData",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BOOKX",
                table: "predictionsData",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MATCHDATE",
                table: "predictionsData",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BOOK1",
                table: "predictionsData");

            migrationBuilder.DropColumn(
                name: "BOOK2",
                table: "predictionsData");

            migrationBuilder.DropColumn(
                name: "BOOKX",
                table: "predictionsData");

            migrationBuilder.DropColumn(
                name: "MATCHDATE",
                table: "predictionsData");
        }
    }
}
