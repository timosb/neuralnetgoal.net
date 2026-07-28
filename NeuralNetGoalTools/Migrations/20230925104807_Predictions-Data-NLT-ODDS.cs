using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeuralNetGoalTools.Migrations
{
    /// <inheritdoc />
    public partial class PredictionsDataNLTODDS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NLT1",
                table: "predictionsData",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NLT2",
                table: "predictionsData",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NLTX",
                table: "predictionsData",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NLT1",
                table: "predictionsData");

            migrationBuilder.DropColumn(
                name: "NLT2",
                table: "predictionsData");

            migrationBuilder.DropColumn(
                name: "NLTX",
                table: "predictionsData");
        }
    }
}
