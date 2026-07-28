using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeuralNetGoalTools.Migrations
{
    /// <inheritdoc />
    public partial class Newextracolsleft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CI",
                table: "predictionsData",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CJ",
                table: "predictionsData",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CK",
                table: "predictionsData",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CI",
                table: "AllData",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CJ",
                table: "AllData",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CK",
                table: "AllData",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CI",
                table: "predictionsData");

            migrationBuilder.DropColumn(
                name: "CJ",
                table: "predictionsData");

            migrationBuilder.DropColumn(
                name: "CK",
                table: "predictionsData");

            migrationBuilder.DropColumn(
                name: "CI",
                table: "AllData");

            migrationBuilder.DropColumn(
                name: "CJ",
                table: "AllData");

            migrationBuilder.DropColumn(
                name: "CK",
                table: "AllData");
        }
    }
}
