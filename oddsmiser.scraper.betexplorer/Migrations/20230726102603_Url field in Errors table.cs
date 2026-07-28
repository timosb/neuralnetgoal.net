using Microsoft.EntityFrameworkCore.Migrations;

namespace oddsmiser.scraper.betexplorer.Migrations
{
    public partial class UrlfieldinErrorstable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ULR",
                table: "Errors",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ULR",
                table: "Errors");
        }
    }
}
