using GemBox.Spreadsheet;
using Tools.NeuralNetGoal.Service.Data;

namespace Tools.NeuralNetGoal.Service.Export;

/// <summary>Column positions of the Excel template (templates/Training_data.xlsx). The order is the file format
/// the neural network expects, ported unchanged from the legacy ExportOddsFreqDataCols enum.</summary>
public enum Col
{
    ID = 0, Country, Division, Seazon, MatchDate, HomeTeam, AwayTeam,
    HomeScore, AwayScore, HalfTimeHomeScore, HalfTimeAwayScore,
    AvgHomeOdds, AvgDrawOdds, AvgAwayOdds,
    Home_Team_Total_Matches_Played, Home_Team_Ranking, Home_Team_Points,
    Home_Team_Matches_Played_At_Home_SoFar, Home_Team_Matches_Played_At_Home_SoFar_Goals_For, Home_Team_Matches_Played_At_Home_SoFar_Goals_Against,
    Home_Team_Matches_Played_Away_SoFar, Home_Team_Matches_Played_Away_SoFar_Goals_For, Home_Team_Matches_Played_Away_SoFar_Goals_Against,
    Away_Team_Total_Matches_Played, Away_Team_Ranking, Away_Team_points,
    Away_Team_Matches_Played_At_Home_SoFar, Away_Team_Matches_Played_At_Home_SoFar_Goals_For, Away_Team_Matches_Played_At_Home_SoFar_Goals_Against,
    Away_Team_Matches_Played_Away_SoFar, Away_Team_Matches_Played_Away_SoFar_Goals_For, Away_Team_Matches_Played_Away_SoFar_Goals_Against,
    HIST_TOTAL_GAMES_BTW, HIST_TOTAL_GAMES_BTW_HOME_WINS, HIST_TOTAL_GAMES_BTW_HOME_WINS_PERC,
    HIST_TOTAL_GAMES_BTW_DRAWS, HIST_TOTAL_GAMES_BTW_DRAWS_PERC,
    HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS, HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS_PERC,
    HIST_TOTAL_GAMES_BTW_HOME_SCORE, HIST_TOTAL_GAMES_BTW_AWAY_SCORE,
    HOME_TEAM_LAST_5_GAMES_WINS, HOME_TEAM_LAST_5_GAMES_WINS_PERC,
    HOME_TEAM_LAST_5_GAMES_DRAWS, HOME_TEAM_LAST_5_GAMES_DRAWS_PERC,
    HOME_TEAM_LAST_5_GAMES_LOSSES, HOME_TEAM_LAST_5_GAMES_LOSSES_PERC,
    AWAY_TEAM_LAST_5_GAMES_WINS, AWAY_TEAM_LAST_5_GAMES_WINS_PERC,
    AWAY_TEAM_LAST_5_GAMES_WINS_DRAWS, AWAY_TEAM_LAST_5_GAMES_DRAWS_PERC,
    AWAY_TEAM_LAST_5_GAMES_LOSSES, AWAY_TEAM_LAST_5_GAMES_LOSSES_PERC,
}

public sealed class ExcelExporter
{
    private readonly string _outputDirectory;
    private readonly string _templatePath;

    public ExcelExporter(string outputDirectory, string gemBoxLicense)
    {
        _outputDirectory = outputDirectory;
        _templatePath = Path.Combine(AppContext.BaseDirectory, "templates", "Training_data.xlsx");
        SpreadsheetInfo.SetLicense(string.IsNullOrWhiteSpace(gemBoxLicense) ? "FREE-LIMITED-KEY" : gemBoxLicense);
    }

    /// <summary>The "final" file: one sheet named "Fixtures" with the feature vector of every upcoming match.</summary>
    public string ExportFixtures(IEnumerable<SoccerDataVector> fixtures)
    {
        var path = PrepareOutputPath($"Fixtures{DateTime.Now:ddMMyy}.xlsx");

        var book = ExcelFile.Load(_templatePath);
        var template = book.Worksheets["template"];

        var ws = book.Worksheets.AddCopy("Fixtures", template);
        Fill(fixtures, ws);

        book.Worksheets.Remove("template");
        book.Save(path);
        return path;
    }

    /// <summary>Training workbook: per league a sheet of past seasons and a "T" sheet with the current season.</summary>
    public string ExportTraining(IEnumerable<SoccerDataVector> vectors, IReadOnlyCollection<RunningLeague> runningLeagues)
    {
        var path = PrepareOutputPath($"OddsFreqProbTrainingData{DateTime.Now:dd-MM-yyHHmmss}.xlsx");

        var book = ExcelFile.Load(_templatePath);
        var template = book.Worksheets["template"];

        foreach (var league in vectors.GroupBy(v => new { v.Country, v.Division }))
        {
            var running = runningLeagues.FirstOrDefault(r => r.Country == league.Key.Country && r.Division == league.Key.Division);
            if (running is null) continue;

            var name = SheetName(league.Key.Country + "-" + league.Key.Division);
            Fill(league.Where(v => v.Seazon != running.Season), book.Worksheets.AddCopy(name, template));
            Fill(league.Where(v => v.Seazon == running.Season), book.Worksheets.AddCopy(SheetName(name + " T"), template));
        }

        book.Worksheets.Remove("template");
        book.Save(path);
        return path;
    }

    /// <summary>Creates the output folder and returns the full path to write. If a file with that name already exists it is
    /// replaced; if it cannot be (for example it is still open in Excel) a time suffix is added instead of failing.</summary>
    private string PrepareOutputPath(string fileName)
    {
        Directory.CreateDirectory(_outputDirectory);
        var path = Path.GetFullPath(Path.Combine(_outputDirectory, fileName));

        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                path = Path.Combine(Path.GetDirectoryName(path)!,
                    $"{Path.GetFileNameWithoutExtension(path)}-{DateTime.Now:HHmmss}{Path.GetExtension(path)}");
            }
            catch (UnauthorizedAccessException)
            {
                path = Path.Combine(Path.GetDirectoryName(path)!,
                    $"{Path.GetFileNameWithoutExtension(path)}-{DateTime.Now:HHmmss}{Path.GetExtension(path)}");
            }
        }

        return path;
    }

    // Excel sheet names are limited to 31 characters.
    private static string SheetName(string s) => s.Length <= 31 ? s : s[..31];

    /// <summary>
    /// Writes the vectors into the template starting at row 1 (row 0 is the header).
    /// NOTE: ported unchanged from the legacy exporter, including its quirks, because the neural network was trained on
    /// this exact layout: the four "score" columns (HomeScore..HalfTimeAwayScore) are REUSED to carry derived features
    /// (points per game and last-5 form of both teams), and those are computed with INTEGER division.
    /// </summary>
    private static void Fill(IEnumerable<SoccerDataVector> data, ExcelWorksheet ws)
    {
        var row = 1;
        foreach (var m in data)
        {
            void Set(Col col, object? value) => ws.Cells[row, (int)col].Value = value;

            Set(Col.Country, m.Country);
            Set(Col.Division, m.Division);
            Set(Col.Seazon, m.Seazon);
            Set(Col.MatchDate, m.MatchDate);
            Set(Col.HomeTeam, m.HomeTeam);
            Set(Col.AwayTeam, m.AwayTeam);

            // LEGACY derived features in the "score" columns (integer division on purpose, see summary above)
            Set(Col.HomeScore, m.Home_Team_Total_Matches_Played is > 0 ? (decimal)((m.Home_Team_Points ?? 0) / m.Home_Team_Total_Matches_Played.Value) : 0m);
            Set(Col.AwayScore, (m.HOME_TEAM_LAST_5_GAMES_WINS * 3 + m.HOME_TEAM_LAST_5_GAMES_DRAWS * 1) / 5);
            Set(Col.HalfTimeHomeScore, m.Away_Team_Total_Matches_Played is > 0 ? m.Away_Team_points / m.Away_Team_Total_Matches_Played : 0);
            Set(Col.HalfTimeAwayScore, (m.AWAY_TEAM_LAST_5_GAMES_WINS * 3 + m.AWAY_TEAM_LAST_5_GAMES_WINS_DRAWS * 1) / 5);

            Set(Col.AvgHomeOdds, m.AvgHomeOdds);
            Set(Col.AvgDrawOdds, m.AvgDrawOdds);
            Set(Col.AvgAwayOdds, m.AvgAwayOdds);
            Set(Col.Home_Team_Total_Matches_Played, m.Home_Team_Total_Matches_Played);
            Set(Col.Home_Team_Ranking, m.Home_Team_Ranking);
            Set(Col.Home_Team_Points, m.Home_Team_Points);
            Set(Col.Home_Team_Matches_Played_At_Home_SoFar, m.Home_Team_Matches_Played_At_Home_SoFar);
            Set(Col.Home_Team_Matches_Played_At_Home_SoFar_Goals_For, m.Home_Team_Matches_Played_At_Home_SoFar_Goals_For);
            Set(Col.Home_Team_Matches_Played_At_Home_SoFar_Goals_Against, m.Home_Team_Matches_Played_At_Home_SoFar_Goals_Against);
            Set(Col.Home_Team_Matches_Played_Away_SoFar, m.Home_Team_Matches_Played_Away_SoFar);
            Set(Col.Home_Team_Matches_Played_Away_SoFar_Goals_For, m.Home_Team_Matches_Played_Away_SoFar_Goals_For);
            Set(Col.Home_Team_Matches_Played_Away_SoFar_Goals_Against, m.Home_Team_Matches_Played_Away_SoFar_Goals_Against);
            Set(Col.Away_Team_Total_Matches_Played, m.Away_Team_Total_Matches_Played);
            Set(Col.Away_Team_Ranking, m.Away_Team_Ranking);
            Set(Col.Away_Team_points, m.Away_Team_points);
            Set(Col.Away_Team_Matches_Played_At_Home_SoFar, m.Away_Team_Matches_Played_At_Home_SoFar);
            Set(Col.Away_Team_Matches_Played_At_Home_SoFar_Goals_For, m.Away_Team_Matches_Played_At_Home_SoFar_Goals_For);
            Set(Col.Away_Team_Matches_Played_At_Home_SoFar_Goals_Against, m.Away_Team_Matches_Played_At_Home_SoFar_Goals_Against);
            Set(Col.Away_Team_Matches_Played_Away_SoFar, m.Away_Team_Matches_Played_Away_SoFar);
            Set(Col.Away_Team_Matches_Played_Away_SoFar_Goals_For, m.Away_Team_Matches_Played_Away_SoFar_Goals_For);
            Set(Col.Away_Team_Matches_Played_Away_SoFar_Goals_Against, m.Away_Team_Matches_Played_Away_SoFar_Goals_Against);
            Set(Col.HIST_TOTAL_GAMES_BTW, m.HIST_TOTAL_GAMES_BTW);
            Set(Col.HIST_TOTAL_GAMES_BTW_HOME_WINS, m.HIST_TOTAL_GAMES_BTW_HOME_WINS);
            Set(Col.HIST_TOTAL_GAMES_BTW_HOME_WINS_PERC, m.HIST_TOTAL_GAMES_BTW_HOME_WINS_PERC);
            Set(Col.HIST_TOTAL_GAMES_BTW_DRAWS, m.HIST_TOTAL_GAMES_BTW_DRAWS);
            Set(Col.HIST_TOTAL_GAMES_BTW_DRAWS_PERC, m.HIST_TOTAL_GAMES_BTW_DRAWS_PERC);
            Set(Col.HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS, m.HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS);
            Set(Col.HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS_PERC, m.HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS_PERC);
            Set(Col.HIST_TOTAL_GAMES_BTW_HOME_SCORE, m.HIST_TOTAL_GAMES_BTW_HOME_SCORE);
            Set(Col.HIST_TOTAL_GAMES_BTW_AWAY_SCORE, m.HIST_TOTAL_GAMES_BTW_AWAY_SCORE);
            Set(Col.HOME_TEAM_LAST_5_GAMES_WINS, m.HOME_TEAM_LAST_5_GAMES_WINS);
            Set(Col.HOME_TEAM_LAST_5_GAMES_WINS_PERC, m.HOME_TEAM_LAST_5_GAMES_WINS_PERC);
            Set(Col.HOME_TEAM_LAST_5_GAMES_DRAWS, m.HOME_TEAM_LAST_5_GAMES_DRAWS);
            Set(Col.HOME_TEAM_LAST_5_GAMES_DRAWS_PERC, m.HOME_TEAM_LAST_5_GAMES_DRAWS_PERC);
            Set(Col.HOME_TEAM_LAST_5_GAMES_LOSSES, m.HOME_TEAM_LAST_5_GAMES_LOSSES);
            Set(Col.HOME_TEAM_LAST_5_GAMES_LOSSES_PERC, m.HOME_TEAM_LAST_5_GAMES_LOSSES_PERC);
            Set(Col.AWAY_TEAM_LAST_5_GAMES_WINS, m.AWAY_TEAM_LAST_5_GAMES_WINS);
            Set(Col.AWAY_TEAM_LAST_5_GAMES_WINS_PERC, m.AWAY_TEAM_LAST_5_GAMES_WINS_PERC);
            Set(Col.AWAY_TEAM_LAST_5_GAMES_WINS_DRAWS, m.AWAY_TEAM_LAST_5_GAMES_WINS_DRAWS);
            Set(Col.AWAY_TEAM_LAST_5_GAMES_DRAWS_PERC, m.AWAY_TEAM_LAST_5_GAMES_DRAWS_PERC);
            Set(Col.AWAY_TEAM_LAST_5_GAMES_LOSSES, m.AWAY_TEAM_LAST_5_GAMES_LOSSES);
            Set(Col.AWAY_TEAM_LAST_5_GAMES_LOSSES_PERC, m.AWAY_TEAM_LAST_5_GAMES_LOSSES_PERC);

            row++;
        }
    }
}
