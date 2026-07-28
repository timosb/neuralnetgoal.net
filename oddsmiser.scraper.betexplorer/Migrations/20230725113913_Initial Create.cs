using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace oddsmiser.scraper.betexplorer.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CountryLeagues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Country = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Division = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Season = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    URL = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryLeagues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataVectors",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Country = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Division = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Seazon = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    MatchDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    HomeTeam = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AwayTeam = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HomeScore = table.Column<int>(type: "int", nullable: true),
                    AwayScore = table.Column<int>(type: "int", nullable: true),
                    HalfTimeHomeScore = table.Column<int>(type: "int", nullable: true),
                    HalfTimeAwayScore = table.Column<int>(type: "int", nullable: true),
                    AvgHomeOdds = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AvgDrawOdds = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AvgAwayOdds = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Home_Team_Total_Matches_Played = table.Column<int>(type: "int", nullable: true),
                    Home_Team_Ranking = table.Column<int>(type: "int", nullable: true),
                    Home_Team_Points = table.Column<int>(type: "int", nullable: true),
                    Home_Team_Matches_Played_At_Home_SoFar = table.Column<int>(type: "int", nullable: true),
                    Home_Team_Matches_Played_At_Home_SoFar_Goals_For = table.Column<int>(type: "int", nullable: true),
                    Home_Team_Matches_Played_At_Home_SoFar_Goals_Against = table.Column<int>(type: "int", nullable: true),
                    Home_Team_Matches_Played_Away_SoFar = table.Column<int>(type: "int", nullable: true),
                    Home_Team_Matches_Played_Away_SoFar_Goals_For = table.Column<int>(type: "int", nullable: true),
                    Home_Team_Matches_Played_Away_SoFar_Goals_Against = table.Column<int>(type: "int", nullable: true),
                    Away_Team_Total_Matches_Played = table.Column<int>(type: "int", nullable: true),
                    Away_Team_Ranking = table.Column<int>(type: "int", nullable: true),
                    Away_Team_points = table.Column<int>(type: "int", nullable: true),
                    Away_Team_Matches_Played_At_Home_SoFar = table.Column<int>(type: "int", nullable: true),
                    Away_Team_Matches_Played_At_Home_SoFar_Goals_For = table.Column<int>(type: "int", nullable: true),
                    Away_Team_Matches_Played_At_Home_SoFar_Goals_Against = table.Column<int>(type: "int", nullable: true),
                    Away_Team_Matches_Played_Away_SoFar = table.Column<int>(type: "int", nullable: true),
                    Away_Team_Matches_Played_Away_SoFar_Goals_For = table.Column<int>(type: "int", nullable: true),
                    Away_Team_Matches_Played_Away_SoFar_Goals_Against = table.Column<int>(type: "int", nullable: true),
                    HIST_TOTAL_GAMES_BTW = table.Column<int>(type: "int", nullable: true),
                    HIST_TOTAL_GAMES_BTW_HOME_WINS = table.Column<int>(type: "int", nullable: true),
                    HIST_TOTAL_GAMES_BTW_HOME_WINS_PERC = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    HIST_TOTAL_GAMES_BTW_DRAWS = table.Column<int>(type: "int", nullable: true),
                    HIST_TOTAL_GAMES_BTW_DRAWS_PERC = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS = table.Column<int>(type: "int", nullable: true),
                    HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS_PERC = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    HIST_TOTAL_GAMES_BTW_HOME_SCORE = table.Column<int>(type: "int", nullable: true),
                    HIST_TOTAL_GAMES_BTW_AWAY_SCORE = table.Column<int>(type: "int", nullable: true),
                    HOME_TEAM_LAST_5_GAMES_WINS = table.Column<int>(type: "int", nullable: true),
                    HOME_TEAM_LAST_5_GAMES_WINS_PERC = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    HOME_TEAM_LAST_5_GAMES_DRAWS = table.Column<int>(type: "int", nullable: true),
                    HOME_TEAM_LAST_5_GAMES_DRAWS_PERC = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    HOME_TEAM_LAST_5_GAMES_LOSSES = table.Column<int>(type: "int", nullable: true),
                    HOME_TEAM_LAST_5_GAMES_LOSSES_PERC = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AWAY_TEAM_LAST_5_GAMES_WINS = table.Column<int>(type: "int", nullable: true),
                    AWAY_TEAM_LAST_5_GAMES_WINS_PERC = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AWAY_TEAM_LAST_5_GAMES_WINS_DRAWS = table.Column<int>(type: "int", nullable: true),
                    AWAY_TEAM_LAST_5_GAMES_DRAWS_PERC = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AWAY_TEAM_LAST_5_GAMES_LOSSES = table.Column<int>(type: "int", nullable: true),
                    AWAY_TEAM_LAST_5_GAMES_LOSSES_PERC = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataVectors", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SoccerFixtures",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Country = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Division = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Seazon = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    MatchDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    HomeTeam = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AwayTeam = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AvgHomeOdds = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AvgDrawOdds = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AvgAwayOdds = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoccerFixtures", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SoccerLeagueTable",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Country = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Division = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Season = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    POS = table.Column<int>(type: "int", nullable: false),
                    MatchDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Team = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Matches_Played = table.Column<int>(type: "int", nullable: false),
                    Matches_Won = table.Column<int>(type: "int", nullable: false),
                    Matches_Drawn = table.Column<int>(type: "int", nullable: false),
                    Matches_Lost = table.Column<int>(type: "int", nullable: false),
                    Goals_For = table.Column<int>(type: "int", nullable: false),
                    Goals_Against = table.Column<int>(type: "int", nullable: false),
                    Goal_Difference = table.Column<int>(type: "int", nullable: false),
                    Home_Matches_Played = table.Column<int>(type: "int", nullable: false),
                    Home_Matches_Won = table.Column<int>(type: "int", nullable: false),
                    Home_Matches_Drawn = table.Column<int>(type: "int", nullable: false),
                    Home_Matches_Lost = table.Column<int>(type: "int", nullable: true),
                    Home_Goals_For = table.Column<int>(type: "int", nullable: true),
                    Home_Goals_Against = table.Column<int>(type: "int", nullable: true),
                    Home_Goal_Difference = table.Column<int>(type: "int", nullable: true),
                    Away_Matches_Played = table.Column<int>(type: "int", nullable: true),
                    Away_Matches_Won = table.Column<int>(type: "int", nullable: true),
                    Away_Matches_Drawn = table.Column<int>(type: "int", nullable: true),
                    Away_Matches_Lost = table.Column<int>(type: "int", nullable: true),
                    Away_Goals_For = table.Column<int>(type: "int", nullable: true),
                    Away_Goals_Against = table.Column<int>(type: "int", nullable: true),
                    Away_Goal_Difference = table.Column<int>(type: "int", nullable: true),
                    Points = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Ranking = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoccerLeagueTable", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SoccerResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Country = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Division = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Season = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    MatchDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HomeTeam = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    AwayTeam = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    HomeScore = table.Column<int>(type: "int", nullable: false),
                    AwayScore = table.Column<int>(type: "int", nullable: false),
                    HalfTimeHomeScore = table.Column<int>(type: "int", nullable: false),
                    HalfTimeAwayScore = table.Column<int>(type: "int", nullable: false),
                    AvgHomeOdds = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AvgDrawOdds = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AvgAwayOdds = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Round = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoccerResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SoccerRunningLeagues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Country = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Division = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Season = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    URL = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoccerRunningLeagues", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CountryLeagues");

            migrationBuilder.DropTable(
                name: "DataVectors");

            migrationBuilder.DropTable(
                name: "SoccerFixtures");

            migrationBuilder.DropTable(
                name: "SoccerLeagueTable");

            migrationBuilder.DropTable(
                name: "SoccerResults");

            migrationBuilder.DropTable(
                name: "SoccerRunningLeagues");
        }
    }
}
