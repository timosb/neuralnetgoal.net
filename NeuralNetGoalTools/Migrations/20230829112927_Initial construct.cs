using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeuralNetGoalTools.Migrations
{
    /// <inheritdoc />
    public partial class Initialconstruct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllData",
                columns: table => new
                {
                    ID = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    COUNTRY = table.Column<string>(type: "TEXT", nullable: true),
                    LEAGUE = table.Column<string>(type: "TEXT", nullable: true),
                    MATCHDATE = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HOME = table.Column<string>(type: "TEXT", nullable: true),
                    AWAY = table.Column<string>(type: "TEXT", nullable: true),
                    FT = table.Column<string>(type: "TEXT", nullable: true),
                    HT = table.Column<string>(type: "TEXT", nullable: true),
                    BOOK_1 = table.Column<decimal>(type: "TEXT", nullable: true),
                    BOOK_X = table.Column<decimal>(type: "TEXT", nullable: true),
                    BOOK_2 = table.Column<decimal>(type: "TEXT", nullable: true),
                    NLT_1 = table.Column<decimal>(type: "TEXT", nullable: true),
                    NLT_X = table.Column<decimal>(type: "TEXT", nullable: true),
                    NLT_2 = table.Column<decimal>(type: "TEXT", nullable: true),
                    NAT_1 = table.Column<decimal>(type: "TEXT", nullable: true),
                    NAT_X = table.Column<decimal>(type: "TEXT", nullable: true),
                    NAT_2 = table.Column<decimal>(type: "TEXT", nullable: true),
                    NFT_1 = table.Column<decimal>(type: "TEXT", nullable: true),
                    NFT_X = table.Column<decimal>(type: "TEXT", nullable: true),
                    NFT_2 = table.Column<decimal>(type: "TEXT", nullable: true),
                    B1 = table.Column<int>(type: "INTEGER", nullable: true),
                    NL1 = table.Column<int>(type: "INTEGER", nullable: true),
                    NF1 = table.Column<int>(type: "INTEGER", nullable: true),
                    NN1 = table.Column<int>(type: "INTEGER", nullable: true),
                    B2 = table.Column<int>(type: "INTEGER", nullable: true),
                    NL2 = table.Column<int>(type: "INTEGER", nullable: true),
                    NF2 = table.Column<int>(type: "INTEGER", nullable: true),
                    NN2 = table.Column<int>(type: "INTEGER", nullable: true),
                    CHART = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllData", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "predictionsData",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    COUNTRY = table.Column<string>(type: "TEXT", nullable: true),
                    LEAGUE = table.Column<string>(type: "TEXT", nullable: true),
                    HOME = table.Column<string>(type: "TEXT", nullable: true),
                    AWAY = table.Column<string>(type: "TEXT", nullable: true),
                    B1 = table.Column<int>(type: "INTEGER", nullable: true),
                    NL1 = table.Column<int>(type: "INTEGER", nullable: true),
                    NF1 = table.Column<int>(type: "INTEGER", nullable: true),
                    NN1 = table.Column<int>(type: "INTEGER", nullable: true),
                    B2 = table.Column<int>(type: "INTEGER", nullable: true),
                    NL2 = table.Column<int>(type: "INTEGER", nullable: true),
                    NF2 = table.Column<int>(type: "INTEGER", nullable: true),
                    NN2 = table.Column<int>(type: "INTEGER", nullable: true),
                    TREND = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_predictionsData", x => x.ID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllData");

            migrationBuilder.DropTable(
                name: "predictionsData");
        }
    }
}
