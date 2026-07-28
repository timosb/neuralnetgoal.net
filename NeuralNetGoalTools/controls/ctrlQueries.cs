using Dapper;
using DevExpress.CodeParser;
using DevExpress.Export;
using DevExpress.Office.Utils;
using DevExpress.Utils.Extensions;
using DevExpress.Utils.Text;
using DevExpress.XtraEditors;
using DevExpress.XtraPrinting;
using DevExpress.XtraRichEdit.Model;
using DevExpress.XtraSplashScreen;
using GemBox.Spreadsheet;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NeuralNetGoalTools.dal;
using NeuralNetGoalTools.models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using static DevExpress.Xpo.Helpers.AssociatedCollectionCriteriaHelper;

namespace NeuralNetGoalTools.controls
{
    public partial class ctrlQueries : DevExpress.XtraEditors.XtraUserControl
    {
        private readonly string _conStr;
        private readonly string _netGoalToolsConStr;
        private readonly AppDbContext _context;
        private List<FavoriteTeamStatisticsAll> favTeamStatsAll;
        private List<Query> Queries { get; set; }

        public ctrlQueries(string conStr, string netGoalToolsConStr)
        {
            InitializeComponent();

            _conStr = conStr;
            _netGoalToolsConStr = netGoalToolsConStr;

            var options = new DbContextOptionsBuilder<AppDbContext>()
               .UseSqlite(netGoalToolsConStr)
               .Options;

            _context = new AppDbContext(options);
        }

        private void btnImportXLS_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //TODO:open file explorer and select file xlsx
            // Create an instance of OpenFileDialog
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // Set filter options and filter index.
            openFileDialog.Filter = "Excel Files|*.xls;*.xlsx";
            openFileDialog.FilterIndex = 1;
            openFileDialog.Multiselect = false;

            // Call the ShowDialog method to show the dialog box.
            DialogResult result = openFileDialog.ShowDialog();

            // Process input if the user clicked OK.
            if (result == DialogResult.OK)
            {
                // Get the selected file's path
                string filePath = openFileDialog.FileName;

                // Perform your operations on the selected file
                var results = ParseFile(filePath);

                if (results == null)
                {
                    XtraMessageBox.Show($"The worksheet is empty or not compatible!",
                              "Problem while reading file!", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return;
                }
                var handle = ShowProgressPanel();
                
                var saveresult = SavePredictionData(results);
                SavePredictionDataToCloud(results);
                getFavoriteTeamStatistics();
                
                CloseProgressPanel(handle);

                if (saveresult)
                    XtraMessageBox.Show($"Data Save Succesfully. n/ Press the Get Results Button!",
                              "Save prediction data!", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            }



        }

        private bool SavePredictionData(List<PredictionsData> predictions)
        {
            try
            {
                using (IDbConnection db = new SqliteConnection(_conStr))
                {
                    db.Open();

                    db.Execute("DELETE FROM PREDICTIONSDATA");
                    foreach (var data in predictions)
                    {
                        string insertQuery = "INSERT INTO PredictionsData (COUNTRY, LEAGUE, HOME, AWAY, B1, NL1, NF1, NN1, B2, NL2, NF2, NN2, TREND, BOOK1, BOOK2,BOOKX, MATCHDATE, NLT1, NLT2, NLTX, CI, CJ, CK) " +
                                             "VALUES (@COUNTRY, @LEAGUE, @HOME, @AWAY, @B1, @NL1, @NF1, @NN1, @B2, @NL2, @NF2, @NN2, @TREND,@BOOK1,@BOOK2,@BOOKX,@MATCHDATE,@NLT1,@NLT2,@NLTX, @CI, @CJ, @CK)";

                        db.Execute(insertQuery, data);
                    }

                    db.Close();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        private bool SavePredictionDataToCloudII(List<PredictionsData> predictions)
        {
            try
            {
                using (IDbConnection db = new SqlConnection(_netGoalToolsConStr))
                {
                    db.Open();

                    db.Execute("TRUNCATE TABLE PREDICTIONSDATA");
                    foreach (var data in predictions)
                    {
                        string insertQuery = "INSERT INTO PredictionsData ([COUNTRY],[LEAGUE],[MATCHDATE],[HOME],[AWAY],[BOOK1],[BOOKX],[BOOK2],[B1],[NL1],[NF1],[NN1],[B2],[NL2],[NF2],[NN2],[TREND]) " +
                                             "VALUES (@COUNTRY,@LEAGUE,@MATCHDATE,@HOME,@AWAY,@BOOK1,@BOOKX,@BOOK2,@B1,@NL1,@NF1,@NN1,@B2,@NL2,@NF2,@NN2,@TREND)";

                        db.Execute(insertQuery, data);
                    }

                    db.Close();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        private bool SavePredictionDataToCloud(List<PredictionsData> predictions)
        {
            try
            {
                using (IDbConnection db = new SqlConnection(_netGoalToolsConStr))
                {
                    db.Open();

                    // Truncate the table before inserting new data
                    db.Execute("TRUNCATE TABLE PREDICTIONSDATA");

                    string insertQuery = "INSERT INTO PredictionsData (COUNTRY, LEAGUE, HOME, AWAY, B1, NL1, NF1, NN1, B2, NL2, NF2, NN2, TREND, BOOK1, BOOK2,BOOKX, MATCHDATE, NLT1, NLT2, NLTX, CI, CJ, CK) " +
                                         "VALUES (@COUNTRY, @LEAGUE, @HOME, @AWAY, @B1, @NL1, @NF1, @NN1, @B2, @NL2, @NF2, @NN2, @TREND,@BOOK1,@BOOK2,@BOOKX,@MATCHDATE,@NLT1,@NLT2,@NLTX, @CI, @CJ, @CK)";

                    // Execute the insert query once with all the data
                    db.Execute(insertQuery, predictions);

                    db.Close();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        private bool SaveAllDataII(List<AllData> allData)
        {
            try
            {
                using (IDbConnection db = new SqliteConnection(_conStr))
                {
                    db.Open();

                    db.Execute("DELETE FROM AllData;DELETE FROM sqlite_sequence WHERE name='AllData';");
                    foreach (var data in allData)
                    {
                        string insertQuery = @"INSERT INTO AllData (COUNTRY, LEAGUE, MATCHDATE, HOME, AWAY, FT, HT, BOOK_1, BOOK_X, BOOK_2, NLT_1, NLT_X, NLT_2," +
                                               "NAT_1, NAT_X, NAT_2, NFT_1, NFT_X, NFT_2, B1, NL1, NF1, NN1, B2, NL2, NF2, NN2, CHART, CI, CK, CJ) " +
                                               "VALUES (@COUNTRY, @LEAGUE, @MATCHDATE, @HOME, @AWAY, @FT, @HT, @BOOK_1, @BOOK_X,@BOOK_2, @NLT_1, @NLT_X, @NLT_2," +
                                               "@NAT_1, @NAT_X, @NAT_2, @NFT_1, @NFT_X, @NFT_2, @B1, @NL1, @NF1, @NN1,@B2, @NL2, @NF2, @NN2, @CHART, @CI, @CK, @CJ)";

                        db.Execute(insertQuery, data);
                    }

                    db.Close();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }
        private bool SaveAllData(List<AllData> allData)
        {
            try
            {
                using (IDbConnection db = new SqliteConnection(_conStr))
                {
                    db.Open();

                    db.Execute("DELETE FROM AllData;DELETE FROM sqlite_sequence WHERE name='AllData';");

                    string insertQuery = @"INSERT INTO AllData (COUNTRY, LEAGUE, MATCHDATE, HOME, AWAY, FT, HT, BOOK_1, BOOK_X, BOOK_2, NLT_1, NLT_X, NLT_2," +
                                          "NAT_1, NAT_X, NAT_2, NFT_1, NFT_X, NFT_2, B1, NL1, NF1, NN1, B2, NL2, NF2, NN2, CHART, CI, CK, CJ) " +
                                          "VALUES (@COUNTRY, @LEAGUE, @MATCHDATE, @HOME, @AWAY, @FT, @HT, @BOOK_1, @BOOK_X,@BOOK_2, @NLT_1, @NLT_X, @NLT_2," +
                                          "@NAT_1, @NAT_X, @NAT_2, @NFT_1, @NFT_X, @NFT_2, @B1, @NL1, @NF1, @NN1,@B2, @NL2, @NF2, @NN2, @CHART, @CI, @CK, @CJ)";

                    // Execute the insert query once with all the data
                    db.Execute(insertQuery, allData);

                    db.Close();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        private List<PredictionsData> GetPredictionsData()
        {
            try
            {
                List<PredictionsData> predictions = null;
                using (IDbConnection db = new SqliteConnection(_conStr))
                {
                    db.Open();
                    string sql = "Select * From PredictionsData ";

                    predictions = db.Query<PredictionsData>(sql).ToList();

                    db.Close();
                }

                return predictions;
            }
            catch (Exception)
            {

                throw;
            }

        }
        private List<AllData> ParseAllData(string filePath)
        {
            // Set GemBox.Spreadsheet license key if you have one
            SpreadsheetInfo.SetLicense("SN-2020Nov05-kLAsb7p1RhzuOPcbbrldwaJd9G5LLgmzIVrtz3GBFIHki2HUw9NYiXGl1LAjxGxXBtms1K4YbATi8Ik0PYPi/Cl6PRw==A");
            // Load Excel file
            var workbook = ExcelFile.Load(filePath);

            // Select the first worksheet
            var worksheet = workbook.Worksheets[0];

            // Create a list to hold the parsed data
            List<AllData> allDataList = new List<AllData>();
            Cursor.Current = Cursors.WaitCursor;
            // Iterate through rows; skip the header row
            for (int i = 1; i < worksheet.Rows.Count; i++)
            {
                var row = worksheet.Rows[i];
                decimal tempDecimal; // Temporary variable for decimal TryParse
                int tempInt; // Temporary variable for int TryParse

                DateTime? matchDate = null;
                if (row.Cells["C"].Value != null && DateTime.TryParse(row.Cells["C"].Value.ToString(), out DateTime parsedDate))
                {
                    matchDate = parsedDate;
                }
                AllData data = new AllData
                {
                    COUNTRY = row.Cells["A"].Value?.ToString(),
                    LEAGUE = row.Cells["B"].Value?.ToString(),
                    HOME = row.Cells["E"].Value?.ToString(),
                    AWAY = row.Cells["F"].Value?.ToString(),
                    MATCHDATE = matchDate,

                    BOOK_1 = (row.Cells["G"].Value != null && decimal.TryParse(row.Cells["G"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,
                    BOOK_X = (row.Cells["H"].Value != null && decimal.TryParse(row.Cells["H"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,
                    BOOK_2 = (row.Cells["I"].Value != null && decimal.TryParse(row.Cells["I"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,

                    NLT_1 = (row.Cells["K"].Value != null && decimal.TryParse(row.Cells["K"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,
                    NLT_X = (row.Cells["L"].Value != null && decimal.TryParse(row.Cells["L"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,
                    NLT_2 = (row.Cells["M"].Value != null && decimal.TryParse(row.Cells["M"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,

                    NAT_1 = (row.Cells["AO"].Value != null && decimal.TryParse(row.Cells["AO"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,
                    NAT_X = (row.Cells["AR"].Value != null && decimal.TryParse(row.Cells["AR"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,
                    NAT_2 = (row.Cells["AQ"].Value != null && decimal.TryParse(row.Cells["AQ"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,

                    NFT_1 = (row.Cells["AS"].Value != null && decimal.TryParse(row.Cells["AS"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,
                    NFT_X = (row.Cells["AT"].Value != null && decimal.TryParse(row.Cells["AT"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,
                    NFT_2 = (row.Cells["AU"].Value != null && decimal.TryParse(row.Cells["AU"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,

                    FT = row.Cells["J"].Value?.ToString(),
                    HT = row.Cells["X"].Value?.ToString(),

                    B1 = (row.Cells["AX"].Value != null && int.TryParse(row.Cells["AX"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,
                    NL1 = (row.Cells["AY"].Value != null && int.TryParse(row.Cells["AY"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,
                    NF1 = (row.Cells["AZ"].Value != null && int.TryParse(row.Cells["AZ"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,
                    NN1 = (row.Cells["BA"].Value != null && int.TryParse(row.Cells["BA"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,

                    B2 = (row.Cells["BB"].Value != null && int.TryParse(row.Cells["BB"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,
                    NL2 = (row.Cells["BC"].Value != null && int.TryParse(row.Cells["BC"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,
                    NF2 = (row.Cells["BD"].Value != null && int.TryParse(row.Cells["BD"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,
                    NN2 = (row.Cells["BE"].Value != null && int.TryParse(row.Cells["BE"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,

                    CHART = row.Cells["CG"].Value?.ToString(),
                    CI = row.Cells["CI"].Value?.ToString(),
                    CJ = row.Cells["CJ"].Value?.ToString(),
                    CK = row.Cells["CK"].Value?.ToString(),




                };

                // Add to list
                allDataList.Add(data);

            }
            Cursor.Current = Cursors.Default;
            return allDataList;
        }

        private List<PredictionsData> ParseFile(string filePath)
        {
            // Set GemBox.Spreadsheet license key if you have one
            SpreadsheetInfo.SetLicense("SN-2020Nov05-kLAsb7p1RhzuOPcbbrldwaJd9G5LLgmzIVrtz3GBFIHki2HUw9NYiXGl1LAjxGxXBtms1K4YbATi8Ik0PYPi/Cl6PRw==A");
            // Load Excel file
            var workbook = ExcelFile.Load(filePath);

            // Select the first worksheet
            var worksheet = workbook.Worksheets[0];

            // Create a list to hold the parsed data
            List<PredictionsData> predictionsDataList = new List<PredictionsData>();
            Cursor.Current = Cursors.WaitCursor;
            // Iterate through rows; skip the header row
            for (int i = 1; i < worksheet.Rows.Count; i++)
            {
                var row = worksheet.Rows[i];
                decimal tempDecimal; // Temporary variable for decimal TryParse
                int tempInt; // Temporary variable for int TryParse

                DateTime? matchDate = null;
                if (row.Cells["C"].Value != null && DateTime.TryParse(row.Cells["C"].Value.ToString(), out DateTime parsedDate))
                {
                    matchDate = parsedDate;
                }
                PredictionsData data = new PredictionsData
                {
                    COUNTRY = row.Cells["A"].Value?.ToString(),
                    LEAGUE = row.Cells["B"].Value?.ToString(),
                    HOME = row.Cells["E"].Value?.ToString(),
                    AWAY = row.Cells["F"].Value?.ToString(),
                    MATCHDATE = matchDate,
                    BOOK1 = (row.Cells["G"].Value != null && decimal.TryParse(row.Cells["G"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,
                    BOOKX = (row.Cells["H"].Value != null && decimal.TryParse(row.Cells["H"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,
                    BOOK2 = (row.Cells["I"].Value != null && decimal.TryParse(row.Cells["I"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,

                    NLT1 = (row.Cells["K"].Value != null && decimal.TryParse(row.Cells["K"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,
                    NLTX = (row.Cells["L"].Value != null && decimal.TryParse(row.Cells["L"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,
                    NLT2 = (row.Cells["M"].Value != null && decimal.TryParse(row.Cells["M"].Value.ToString(), out tempDecimal)) ? tempDecimal : (decimal?)null,

                    B1 = (row.Cells["AX"].Value != null && int.TryParse(row.Cells["AX"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,
                    NL1 = (row.Cells["AY"].Value != null && int.TryParse(row.Cells["AY"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,
                    NF1 = (row.Cells["AZ"].Value != null && int.TryParse(row.Cells["AZ"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,
                    NN1 = (row.Cells["BA"].Value != null && int.TryParse(row.Cells["BA"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,

                    B2 = (row.Cells["BB"].Value != null && int.TryParse(row.Cells["BB"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,
                    NL2 = (row.Cells["BC"].Value != null && int.TryParse(row.Cells["BC"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,
                    NF2 = (row.Cells["BD"].Value != null && int.TryParse(row.Cells["BD"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,
                    NN2 = (row.Cells["BE"].Value != null && int.TryParse(row.Cells["BE"].Value.ToString(), out tempInt)) ? tempInt : (int?)null,

                    TREND = row.Cells["CG"].Value?.ToString(),
                    CI = row.Cells["CI"].Value?.ToString(),
                    CJ = row.Cells["CJ"].Value?.ToString(),
                    CK = row.Cells["CK"].Value?.ToString()

                };

                // Add to list
                predictionsDataList.Add(data);

            }
            Cursor.Current = Cursors.Default;
            return predictionsDataList;
        }

        private void getFavoriteTeamStatistics()
        {
            using (IDbConnection db = new SqlConnection(_netGoalToolsConStr))
            {
                favTeamStatsAll = db.Query<FavoriteTeamStatisticsAll>("dbo.GET_TEAM_IN_FAVOR_STATS_ALL", commandType: CommandType.StoredProcedure, commandTimeout: 300).ToList();

            }
        }

        private async Task<List<FavoriteTeamStatisticsAll>> getFavoriteTeamStatisticsAsynch()
        {
            using (IDbConnection db = new SqlConnection(_netGoalToolsConStr))
            {
                var q = await db.QueryAsync<FavoriteTeamStatisticsAll>("dbo.GET_TEAM_IN_FAVOR_STATS_ALL", commandType: CommandType.StoredProcedure, commandTimeout: 300);
                return q.ToList();
            }
        }

        private void btnGetData_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                var qResults = GetQueryResults();
                var PredData = GetPredictionsData();
                var results = new List<TotalResults>();

                using (IDbConnection db = new SqlConnection(_netGoalToolsConStr))
                {

                    foreach (var item in PredData)
                    {
                        string favTeam = string.Empty;
                        if (item.BOOK1.Value < item.BOOKX.Value && item.BOOK1.Value < item.BOOK2.Value)
                            favTeam = item.HOME;
                        if (item.BOOK2.Value < item.BOOKX.Value && item.BOOK2.Value < item.BOOK1.Value)
                            favTeam = item.AWAY;

                        var queryMatchResult = qResults.Where(x => x.COUNTRY == item.COUNTRY && x.LEAGUE == item.LEAGUE && x.HOME == item.HOME && x.AWAY == item.AWAY).FirstOrDefault();

                        //var favTeamStats = db.Query<FavoriteTeamStatistics>($"EXEC dbo.GET_TEAM_IN_FAVOR_STATS '{item.COUNTRY}','{item.LEAGUE}','{favTeam}','{item.MATCHDATE.Value.ToString("yyyy-MM-dd HH:mm")}'").FirstOrDefault();
                        //var favTeamGames = db.Query<TeamInFavorPastGames>($"EXEC dbo.GET_TEAM_IN_FAVOR_GAMES '{item.COUNTRY}','{item.LEAGUE}','{favTeam}','{item.MATCHDATE.Value.ToString("yyyy-MM-dd HH:mm")}'").ToList();

                        var match = favTeamStatsAll.FirstOrDefault(x => x.COUNTRY == item.COUNTRY && x.LEAGUE == item.LEAGUE && x.HOME == item.HOME && x.AWAY == item.AWAY && x.MATCHDATE == item.MATCHDATE);

                        results.Add(new TotalResults
                        {
                            AWAY = item.AWAY,
                            AWINS = queryMatchResult.AWINS,
                            COUNTRY = item.COUNTRY,
                            DRAWS = queryMatchResult.DRAWS,
                            BOOK1 = item.BOOK1.Value,
                            BOOK2 = item.BOOK2.Value,
                            BOOKX = item.BOOKX.Value,
                            GG = queryMatchResult.GG,
                            HOME = item.HOME,
                            HWINS = queryMatchResult.HWINS,
                            LEAGUE = item.LEAGUE,
                            NG = queryMatchResult.NG,
                            OVER = queryMatchResult.OVER,
                            UNDER = queryMatchResult.UNDER,
                            Draws = favTeamStatsAll.Where(x => x.COUNTRY == item.COUNTRY && x.LEAGUE == item.LEAGUE && x.HOME == item.HOME && x.AWAY == item.AWAY && x.MATCHDATE == item.MATCHDATE).FirstOrDefault()?.Draws,
                            Losses = favTeamStatsAll.Where(x => x.COUNTRY == item.COUNTRY && x.LEAGUE == item.LEAGUE && x.HOME == item.HOME && x.AWAY == item.AWAY && x.MATCHDATE == item.MATCHDATE).FirstOrDefault()?.Losses,
                            Matches = favTeamStatsAll.Where(x => x.COUNTRY == item.COUNTRY && x.LEAGUE == item.LEAGUE && x.HOME == item.HOME && x.AWAY == item.AWAY && x.MATCHDATE == item.MATCHDATE).FirstOrDefault()?.Matches,
                            Wins = favTeamStatsAll.Where(x => x.COUNTRY == item.COUNTRY && x.LEAGUE == item.LEAGUE && x.HOME == item.HOME && x.AWAY == item.AWAY && x.MATCHDATE == item.MATCHDATE).FirstOrDefault()?.Wins,
                            History = queryMatchResult.Matches
                        });

                    }

                    db.Close();
                }
                Cursor.Current = Cursors.Default;
                totalResultsBindingSource.DataSource = results;
            }
            catch (Exception ex)
            {

                throw ex;
            }
        }

        private List<QueryResults> GetQueryResults()
        {
            try
            {


                var sqlPredictionGames = @$"SELECT * FROM PredictionsData";

                var result = new List<QueryResults>();

                using (IDbConnection db = new SqliteConnection(_conStr))
                {
                    db.Open();

                    var games = db.Query<PredictionsData>(sqlPredictionGames).ToList();

                    foreach (var match in games)
                    {

                        var conditions = new List<string>();

                        if (switchCountry.Checked)
                            conditions.Add($"COUNTRY = '{match.COUNTRY}'");
                        if (switchLeague.Checked)
                            conditions.Add($"LEAGUE = '{match.LEAGUE}'");
                        if (switchHome.Checked)
                            conditions.Add($"HOME = '{match.HOME}'");
                        if (switchAway.Checked)
                            conditions.Add($"AWAY = '{match.AWAY}'");
                        if (switchB1.Checked)
                            conditions.Add($"IFNULL(B1,0) = IFNULL({match.B1.DBNullIfNull()},0) AND IFNULL(NL1,0) = IFNULL({match.NL1.DBNullIfNull()},0) AND IFNULL(NF1,0) = IFNULL({match.NF1.DBNullIfNull()},0) AND IFNULL(NN1,0) = IFNULL({match.NN1.DBNullIfNull()},0)");
                        if (switchB2.Checked)
                            conditions.Add($"IFNULL(B2,0) = IFNULL({match.B2.DBNullIfNull()},0) AND IFNULL(NL2,0) = IFNULL({match.NL2.DBNullIfNull()},0) AND IFNULL(NF2,0) = IFNULL({match.NF2.DBNullIfNull()},0) AND IFNULL(NN2,0) = IFNULL({match.NN2.DBNullIfNull()},0)");
                        if (switchChart.Checked)
                            conditions.Add($"CHART = '{match.TREND}'");
                        if (useTime.Checked)
                            conditions.Add($"time(MATCHDATE) >= time('{match.MATCHDATE.Value.ToString("yyyy-MM-dd HH:mm")}')");
                        if (swch1du.Checked)
                            conditions.Add($"CI = '{match.CI}'");
                        if (swchXdu.Checked)
                            conditions.Add($"CJ = '{match.CJ}'");
                        if (swch2du.Checked)
                            conditions.Add($"CK = '{match.CK}'");

                        var sqlWhere = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";


                        if (sqlWhere.Length > 0)
                        {
                            // Remove the last " AND" if it exists
                            if (sqlWhere.EndsWith(" AND"))
                            {
                                sqlWhere = sqlWhere.Remove(sqlWhere.Length - 4);
                            }
                        }
                        else
                        {
                            XtraMessageBox.Show($"You must select at least one parameter!",
                                                      "Problem procecisng the paramters!", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                            return null;

                        }

                        var sqlHistory = @$"SELECT COUNTRY,LEAGUE,MATCHDATE,HOME,AWAY,BOOK_1 as BOOK1,BOOK_X as BOOKX,BOOK_2 as BOOK2,ROUND(NLT_1, 2) AS NLT1,ROUND(NLT_X, 2) AS NLTX,ROUND(NLT_2, 2) AS NLT2,CAST(SUBSTR(FT, 1, 1) AS INTEGER) as HomeScore,CAST(SUBSTR(FT, -1) AS INTEGER) as AwayScore from AllData {sqlWhere}";
                        var history = db.Query<QueryMatches>(sqlHistory).ToList();

                        result.Add(new QueryResults()
                        {
                            AWAY = match.AWAY,
                            AWINS = history.Where(x => x.HomeScore < x.AwayScore).Count(),
                            DRAWS = history.Where(x => x.HomeScore == x.AwayScore).Count(),
                            GG = history.Where(x => x.HomeScore > 0 && x.AwayScore > 0).Count(),
                            HWINS = history.Where(x => x.HomeScore > x.AwayScore).Count(),
                            LEAGUE = match.LEAGUE,
                            HOME = match.HOME,
                            NG = history.Where(x => x.HomeScore == 0 && x.AwayScore == 0).Count(),
                            OVER = history.Where(x => x.HomeScore + x.AwayScore > 2.5).Count(),
                            UNDER = history.Where(x => x.HomeScore + x.AwayScore < 2.5).Count(),
                            COUNTRY = match.COUNTRY,
                            Matches = history

                        });

                    }

                    db.Close();
                }

                return result;
            }
            catch (Exception)
            {

                throw;
            }

        }

        private async Task<List<QueryResults>> GetQueryResults(Query q)
        {
            try
            {


                var sqlPredictionGames = @$"SELECT * FROM PredictionsData";

                var result = new List<QueryResults>();

                using (IDbConnection db = new SqliteConnection(_conStr))
                {
                    db.Open();

                    var games = await db.QueryAsync<PredictionsData>(sqlPredictionGames);

                    foreach (var match in games)
                    {

                        var conditions = new List<string>();

                        if (q.Country)
                            conditions.Add($"COUNTRY = '{match.COUNTRY}'");
                        if (q.League)
                            conditions.Add($"LEAGUE = '{match.LEAGUE}'");
                        if (q.HomeTeam)
                            conditions.Add($"HOME = '{match.HOME}'");
                        if (q.AwayTeam)
                            conditions.Add($"AWAY = '{match.AWAY}'");
                        if (q.B1NL1NF1NN1)
                            conditions.Add($"IFNULL(B1,0) = IFNULL({match.B1.DBNullIfNull()},0) AND IFNULL(NL1,0) = IFNULL({match.NL1.DBNullIfNull()},0) AND IFNULL(NF1,0) = IFNULL({match.NF1.DBNullIfNull()},0) AND IFNULL(NN1,0) = IFNULL({match.NN1.DBNullIfNull()},0)");
                        if (q.B2NL2BF2NN2)
                            conditions.Add($"IFNULL(B2,0) = IFNULL({match.B2.DBNullIfNull()},0) AND IFNULL(NL2,0) = IFNULL({match.NL2.DBNullIfNull()},0) AND IFNULL(NF2,0) = IFNULL({match.NF2.DBNullIfNull()},0) AND IFNULL(NN2,0) = IFNULL({match.NN2.DBNullIfNull()},0)");
                        if (q.Chart)
                            conditions.Add($"CHART = '{match.TREND}'");
                        if (q.UseTime)
                            conditions.Add($"time(MATCHDATE) >= time('{match.MATCHDATE.Value.ToString("yyyy-MM-dd HH:mm")}')");
                        if (q.DU1)
                            conditions.Add($"CI = '{match.CI}'");
                        if (q.DUX)
                            conditions.Add($"CJ = '{match.CJ}'");
                        if (q.DU2)
                            conditions.Add($"CK = '{match.CK}'");

                        var sqlWhere = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";


                        if (sqlWhere.Length > 0)
                        {
                            // Remove the last " AND" if it exists
                            if (sqlWhere.EndsWith(" AND"))
                            {
                                sqlWhere = sqlWhere.Remove(sqlWhere.Length - 4);
                            }
                        }
                        else
                        {
                            XtraMessageBox.Show($"You must select at least one parameter!",
                                                      "Problem procecisng the paramters!", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                            return null;

                        }

                        var sqlHistory = @$"SELECT COUNTRY,LEAGUE,MATCHDATE,HOME,AWAY,BOOK_1 as BOOK1,BOOK_X as BOOKX,BOOK_2 as BOOK2,NLT_1 as NLT1,NLT_X as NLTX,NLT_2 as NLT2,CAST(SUBSTR(FT, 1, 1) AS INTEGER) as HomeScore,CAST(SUBSTR(FT, -1) AS INTEGER) as AwayScore from AllData {sqlWhere}";
                        var history = await db.QueryAsync<QueryMatches>(sqlHistory);

                        result.Add(new QueryResults()
                        {
                            AWAY = match.AWAY,
                            AWINS = history.Where(x => x.HomeScore < x.AwayScore).Count(),
                            DRAWS = history.Where(x => x.HomeScore == x.AwayScore).Count(),
                            GG = history.Where(x => x.HomeScore > 0 && x.AwayScore > 0).Count(),
                            HWINS = history.Where(x => x.HomeScore > x.AwayScore).Count(),
                            LEAGUE = match.LEAGUE,
                            HOME = match.HOME,
                            MATCHDATE = match.MATCHDATE,
                            NG = history.Where(x => x.HomeScore == 0 || x.AwayScore == 0).Count(),
                            OVER = history.Where(x => x.HomeScore + x.AwayScore > 2.5).Count(),
                            UNDER = history.Where(x => x.HomeScore + x.AwayScore < 2.5).Count(),
                            COUNTRY = match.COUNTRY,
                            Matches = history.ToList()

                        });

                    }

                    db.Close();
                }

                return result;
            }
            catch (Exception)
            {

                throw;
            }

        }

        private List<QueryResults> GetQueryResultsII()
        {
            try
            {
                var conditions = new List<string>();

                if (switchCountry.Checked)
                    conditions.Add("COUNTRY = PD.COUNTRY");
                if (switchLeague.Checked)
                    conditions.Add("LEAGUE = PD.LEAGUE");
                if (switchHome.Checked)
                    conditions.Add("HOME = PD.HOME");
                if (switchAway.Checked)
                    conditions.Add("AWAY = PD.AWAY");
                if (switchB1.Checked)
                    conditions.Add("IFNULL(B1,0) = IFNULL(PD.B1,0) AND IFNULL(NL1,0) = IFNULL(PD.NL1,0) AND IFNULL(NF1,0) = IFNULL(PD.NF1,0) AND IFNULL(NN1,0) = IFNULL(PD.NN1,0)");
                if (switchB2.Checked)
                    conditions.Add("IFNULL(B2,0) = IFNULL(PD.B2,0) AND IFNULL(NL2,0) = IFNULL(PD.NL2,0) AND IFNULL(NF2,0) = IFNULL(PD.NF2,0) AND IFNULL(NN2,0) = IFNULL(PD.NN2,0)");
                if (switchChart.Checked)
                    conditions.Add("CHART = pd.TREND");
                if (useTime.Checked)
                    conditions.Add($"time(MATCHDATE) >= time(PD.MATCHDATE)");
                if (swch1du.Checked)
                    conditions.Add($"CI = PD.CI");
                if (swchXdu.Checked)
                    conditions.Add($"CJ = PD.CJ");
                if (swch2du.Checked)
                    conditions.Add($"CK = PD.CK");

                var sqlWhere = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";


                if (sqlWhere.Length > 0)
                {
                    // Remove the last " AND" if it exists
                    if (sqlWhere.EndsWith(" AND"))
                    {
                        sqlWhere = sqlWhere.Remove(sqlWhere.Length - 4);
                    }
                }
                else
                {
                    XtraMessageBox.Show($"You must select at least one parameter!",
                                              "Problem procecisng the paramters!", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return null;

                }

                var sql = @$"SELECT PD.COUNTRY, PD.LEAGUE, PD.HOME, PD.AWAY, PD.TREND,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) > CAST(SUBSTR(FT, -1) AS INTEGER) THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS HWINS,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) = CAST(SUBSTR(FT, -1) AS INTEGER) THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS DRAWS,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) < CAST(SUBSTR(FT, -1) AS INTEGER) THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS AWINS,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) + CAST(SUBSTR(FT, -1) AS INTEGER) < 2.5 THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS UNDER,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) + CAST(SUBSTR(FT, -1) AS INTEGER) > 2.5 THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS OVER,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) > 0 AND CAST(SUBSTR(FT, -1) AS INTEGER) > 0 THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS GG,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) > 0 AND CAST(SUBSTR(FT, -1) AS INTEGER) > 0 THEN 0 ELSE 1 END) FROM AllData {sqlWhere}) AS NG
                FROM PredictionsData PD
                ";

                var result = new List<QueryResults>();

                using (IDbConnection db = new SqliteConnection(_conStr))
                {
                    db.Open();

                    result = db.Query<QueryResults>(sql).ToList();

                    db.Close();
                }

                return result;
            }
            catch (Exception)
            {

                throw;
            }

        }

        private List<QueryResults> queryCountryLeagueb1b2chart()
        {
            try
            {
                var conditions = new List<string>();
                conditions.Add("COUNTRY = PD.COUNTRY");
                conditions.Add("LEAGUE = PD.LEAGUE");
                conditions.Add("IFNULL(B1,0) = IFNULL(PD.B1,0) AND IFNULL(NL1,0) = IFNULL(PD.NL1,0) AND IFNULL(NF1,0) = IFNULL(PD.NF1,0) AND IFNULL(NN1,0) = IFNULL(PD.NN1,0)");
                conditions.Add("IFNULL(B2,0) = IFNULL(PD.B2,0) AND IFNULL(NL2,0) = IFNULL(PD.NL2,0) AND IFNULL(NF2,0) = IFNULL(PD.NF2,0) AND IFNULL(NN2,0) = IFNULL(PD.NN2,0)");
                conditions.Add("CHART = pd.TREND");

                var sqlWhere = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";


                if (sqlWhere.Length > 0)
                {
                    // Remove the last " AND" if it exists
                    if (sqlWhere.EndsWith(" AND"))
                    {
                        sqlWhere = sqlWhere.Remove(sqlWhere.Length - 4);
                    }
                }
                else
                {
                    XtraMessageBox.Show($"You must select at least one parameter!",
                                              "Problem procecisng the paramters!", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return null;

                }

                var sql = @$"SELECT PD.COUNTRY, PD.LEAGUE, PD.HOME, PD.AWAY, PD.TREND,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) > CAST(SUBSTR(FT, -1) AS INTEGER) THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS HWINS,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) = CAST(SUBSTR(FT, -1) AS INTEGER) THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS DRAWS,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) < CAST(SUBSTR(FT, -1) AS INTEGER) THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS AWINS,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) + CAST(SUBSTR(FT, -1) AS INTEGER) < 2.5 THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS UNDER,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) + CAST(SUBSTR(FT, -1) AS INTEGER) > 2.5 THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS OVER,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) > 0 AND CAST(SUBSTR(FT, -1) AS INTEGER) > 0 THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS GG,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) > 0 AND CAST(SUBSTR(FT, -1) AS INTEGER) > 0 THEN 0 ELSE 1 END) FROM AllData {sqlWhere}) AS NG
                FROM PredictionsData PD
                ";

                var result = new List<QueryResults>();

                using (IDbConnection db = new SqliteConnection(_conStr))
                {
                    db.Open();

                    result = db.Query<QueryResults>(sql).ToList();

                    db.Close();
                }

                return result;
            }
            catch (Exception)
            {

                throw;
            }

        }

        private List<QueryResults> queryb1b2chart()
        {
            try
            {
                var conditions = new List<string>();
                conditions.Add("IFNULL(B1,0) = IFNULL(PD.B1,0) AND IFNULL(NL1,0) = IFNULL(PD.NL1,0) AND IFNULL(NF1,0) = IFNULL(PD.NF1,0) AND IFNULL(NN1,0) = IFNULL(PD.NN1,0)");
                conditions.Add("IFNULL(B2,0) = IFNULL(PD.B2,0) AND IFNULL(NL2,0) = IFNULL(PD.NL2,0) AND IFNULL(NF2,0) = IFNULL(PD.NF2,0) AND IFNULL(NN2,0) = IFNULL(PD.NN2,0)");
                conditions.Add("CHART = pd.TREND");

                var sqlWhere = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

                if (sqlWhere.Length > 0)
                {
                    // Remove the last " AND" if it exists
                    if (sqlWhere.EndsWith(" AND"))
                    {
                        sqlWhere = sqlWhere.Remove(sqlWhere.Length - 4);
                    }
                }
                else
                {
                    XtraMessageBox.Show($"You must select at least one parameter!",
                                              "Problem procecisng the paramters!", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return null;

                }

                var sql = @$"SELECT PD.COUNTRY, PD.LEAGUE, PD.HOME, PD.AWAY, PD.TREND,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) > CAST(SUBSTR(FT, -1) AS INTEGER) THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS HWINS,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) = CAST(SUBSTR(FT, -1) AS INTEGER) THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS DRAWS,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) < CAST(SUBSTR(FT, -1) AS INTEGER) THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS AWINS,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) + CAST(SUBSTR(FT, -1) AS INTEGER) < 2.5 THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS UNDER,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) + CAST(SUBSTR(FT, -1) AS INTEGER) > 2.5 THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS OVER,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) > 0 AND CAST(SUBSTR(FT, -1) AS INTEGER) > 0 THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS GG,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) > 0 AND CAST(SUBSTR(FT, -1) AS INTEGER) > 0 THEN 0 ELSE 1 END) FROM AllData {sqlWhere}) AS NG
                FROM PredictionsData PD
                ";

                var result = new List<QueryResults>();

                using (IDbConnection db = new SqliteConnection(_conStr))
                {
                    db.Open();

                    result = db.Query<QueryResults>(sql).ToList();

                    db.Close();
                }

                return result;
            }
            catch (Exception)
            {

                throw;
            }


        }

        private List<QueryResults> queryCountryLeagueb1b2()
        {
            try
            {
                var conditions = new List<string>();
                conditions.Add("COUNTRY = PD.COUNTRY");
                conditions.Add("LEAGUE = PD.LEAGUE");
                conditions.Add("IFNULL(B1,0) = IFNULL(PD.B1,0) AND IFNULL(NL1,0) = IFNULL(PD.NL1,0) AND IFNULL(NF1,0) = IFNULL(PD.NF1,0) AND IFNULL(NN1,0) = IFNULL(PD.NN1,0)");
                conditions.Add("IFNULL(B2,0) = IFNULL(PD.B2,0) AND IFNULL(NL2,0) = IFNULL(PD.NL2,0) AND IFNULL(NF2,0) = IFNULL(PD.NF2,0) AND IFNULL(NN2,0) = IFNULL(PD.NN2,0)");

                var sqlWhere = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";


                if (sqlWhere.Length > 0)
                {
                    // Remove the last " AND" if it exists
                    if (sqlWhere.EndsWith(" AND"))
                    {
                        sqlWhere = sqlWhere.Remove(sqlWhere.Length - 4);
                    }
                }
                else
                {
                    XtraMessageBox.Show($"You must select at least one parameter!",
                                              "Problem procecisng the paramters!", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return null;

                }

                var sql = @$"SELECT PD.COUNTRY, PD.LEAGUE, PD.HOME, PD.AWAY, PD.TREND,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) > CAST(SUBSTR(FT, -1) AS INTEGER) THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS HWINS,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) = CAST(SUBSTR(FT, -1) AS INTEGER) THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS DRAWS,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) < CAST(SUBSTR(FT, -1) AS INTEGER) THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS AWINS,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) + CAST(SUBSTR(FT, -1) AS INTEGER) < 2.5 THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS UNDER,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) + CAST(SUBSTR(FT, -1) AS INTEGER) > 2.5 THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS OVER,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) > 0 AND CAST(SUBSTR(FT, -1) AS INTEGER) > 0 THEN 1 ELSE 0 END) FROM AllData {sqlWhere}) AS GG,
                (SELECT SUM(CASE WHEN CAST(SUBSTR(FT, 1, 1) AS INTEGER) > 0 AND CAST(SUBSTR(FT, -1) AS INTEGER) > 0 THEN 0 ELSE 1 END) FROM AllData {sqlWhere}) AS NG
                FROM PredictionsData PD
                ";

                var result = new List<QueryResults>();

                using (IDbConnection db = new SqliteConnection(_conStr))
                {
                    db.Open();

                    result = db.Query<QueryResults>(sql).ToList();

                    db.Close();
                }

                return result;
            }
            catch (Exception)
            {

                throw;
            }

        }



        private void btnExport_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), $"{DateTime.Now.ToString("ddMMyy")}.PredictionResults.xlsx");
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                XlsxExportOptionsEx options = new XlsxExportOptionsEx
                {
                    ExportType = ExportType.WYSIWYG
                };

                grdQueries.ExportToXlsx(path, options);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

            }
            catch (Exception ex)
            {
                XtraMessageBox.Show("Something went wrong please try again later!",
                                    "Problem while saving data!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnImportAll_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            //TODO:open file explorer and select file xlsx
            // Create an instance of OpenFileDialog
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // Set filter options and filter index.
            openFileDialog.Filter = "Excel Files|*.xls;*.xlsx";
            openFileDialog.FilterIndex = 1;
            openFileDialog.Multiselect = false;

            // Call the ShowDialog method to show the dialog box.
            DialogResult result = openFileDialog.ShowDialog();

            // Process input if the user clicked OK.
            if (result == DialogResult.OK)
            {
                // Get the selected file's path
                string filePath = openFileDialog.FileName;
                Cursor.Current = Cursors.WaitCursor;
                // Perform your operations on the selected file
                var results = ParseAllData(filePath);

                if (results == null)
                {
                    XtraMessageBox.Show($"The worksheet is empty or not compatible!",
                              "Problem while reading file!", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return;
                }

                var saveresult = SaveAllData(results);
                Cursor.Current = Cursors.Default;
                if (saveresult)
                    XtraMessageBox.Show($"Data Save Succesfully. n/ Press the Get Results Button!",
                              "Save prediction data!", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            }
        }

        private void btnSummary_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            try
            {

                Cursor.Current = Cursors.WaitCursor;
                var clb1b2cResults = queryCountryLeagueb1b2chart();
                var b1b2cResults = queryb1b2chart();
                var clb1b2Results = queryCountryLeagueb1b2();

                var results = new List<SummaryModel>();

                using (IDbConnection db = new SqlConnection(_netGoalToolsConStr))
                {
                    db.Open();

                    var prevGamesSQL = $"SELECT [ID],[Country],[Division],[Seazon],[MatchDate],[HomeTeam],[AwayTeam],[AvgHomeOdds],[AvgDrawOdds],[AvgAwayOdds],HG.Goals as [HOME_GOALS],AG.Goals as [AWAY_GOALS]" +
                        $" FROM [neuralnetGoalData].[dbo].[SoccerFixtures]" +
                        $" outer apply [dbo].[FN_SOCCER_MATCH_TEAM_GOALS] (MatchDate,HomeTeam,Country,Division,Seazon,4) HG" +
                        $" outer apply [dbo].[FN_SOCCER_MATCH_TEAM_GOALS] (MatchDate,AwayTeam,Country,Division,Seazon,4) AG";

                    var prevGamesScores = db.Query<PrevGameScore>(prevGamesSQL).ToList();

                    //string sql = "Select * From PredictionsData ";

                    var PredData = GetPredictionsData();

                    if (PredData == null || PredData.Count() == 0)
                    {
                        XtraMessageBox.Show($"Try to import the file with predition data again!",
                                      "Problem getting prediction data!", MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                        return;
                    }


                    foreach (var item in PredData)
                    {
                        string favTeam = string.Empty;
                        if (item.BOOK1.Value < item.BOOKX.Value && item.BOOK1.Value < item.BOOK2.Value)
                            favTeam = item.HOME;
                        if (item.BOOK2.Value < item.BOOKX.Value && item.BOOK2.Value < item.BOOK1.Value)
                            favTeam = item.AWAY;

                        var clb1b2cItem = clb1b2cResults.Where(x => x.COUNTRY == item.COUNTRY && x.LEAGUE == item.LEAGUE && x.HOME == item.HOME && x.AWAY == item.AWAY).FirstOrDefault();
                        var b1b2cItem = b1b2cResults.Where(x => x.COUNTRY == item.COUNTRY && x.LEAGUE == item.LEAGUE && x.HOME == item.HOME && x.AWAY == item.AWAY).FirstOrDefault();
                        var clb1b2Item = clb1b2Results.Where(x => x.COUNTRY == item.COUNTRY && x.LEAGUE == item.LEAGUE && x.HOME == item.HOME && x.AWAY == item.AWAY).FirstOrDefault();

                        var favTeamStats = db.Query<FavoriteTeamStatistics>($"EXEC dbo.GET_TEAM_IN_FAVOR_STATS '{item.COUNTRY}','{item.LEAGUE}','{favTeam}','{item.MATCHDATE.Value.ToString("yyyy-MM-dd HH:mm")}'").FirstOrDefault();
                        //var favTeamGames = db.Query<TeamInFavorPastGames>($"EXEC dbo.GET_TEAM_IN_FAVOR_GAMES '{item.COUNTRY}','{item.LEAGUE}','{favTeam}','{item.MATCHDATE.Value.ToString("yyyy-MM-dd HH:mm")}'").ToList();

                        var CLB1B2ChGames = (clb1b2cItem == null) ? 0 : clb1b2cItem.HWINS.ZeroIfNull() + clb1b2cItem.DRAWS.ZeroIfNull() + clb1b2cItem.AWINS.ZeroIfNull();
                        var CLB1B2Ch1 = (clb1b2cItem == null) ? 0 : (double)clb1b2cItem.HWINS.ZeroIfNull() / CLB1B2ChGames.OneIfZero();
                        var CLB1B2ChX = (clb1b2cItem == null) ? 0 : (double)clb1b2cItem.DRAWS.ZeroIfNull() / CLB1B2ChGames.OneIfZero();
                        var CLB1B2Ch2 = (clb1b2cItem == null) ? 0 : (double)clb1b2cItem.AWINS.ZeroIfNull() / CLB1B2ChGames.OneIfZero();

                        var B1B2ChGames = (b1b2cItem == null) ? 0 : b1b2cItem.HWINS.ZeroIfNull() + b1b2cItem.DRAWS.ZeroIfNull() + b1b2cItem.AWINS.ZeroIfNull();
                        var B1B2Ch1 = (b1b2cItem == null) ? 0 : (double)b1b2cItem.HWINS.ZeroIfNull() / B1B2ChGames.OneIfZero();
                        var B1B2ChX = (b1b2cItem == null) ? 0 : (double)b1b2cItem.DRAWS.ZeroIfNull() / B1B2ChGames.OneIfZero();
                        var B1B2Ch2 = (b1b2cItem == null) ? 0 : (double)b1b2cItem.AWINS.ZeroIfNull() / B1B2ChGames.OneIfZero();

                        var CLB1B2Games = (clb1b2Item == null) ? 0 : clb1b2Item.HWINS.ZeroIfNull() + clb1b2Item.DRAWS.ZeroIfNull() + clb1b2Item.AWINS.ZeroIfNull();
                        var CLB1B21 = (clb1b2Item == null) ? 0 : (double)clb1b2Item.HWINS.ZeroIfNull() / CLB1B2Games.OneIfZero();
                        var CLB1B2X = (clb1b2Item == null) ? 0 : (double)clb1b2Item.DRAWS.ZeroIfNull() / CLB1B2Games.OneIfZero();
                        var CLB1B22 = (clb1b2Item == null) ? 0 : (double)clb1b2Item.AWINS.ZeroIfNull() / CLB1B2Games.OneIfZero();

                        var FavorHistoryGames = (favTeamStats == null) ? (long?)0 : favTeamStats.Matches;
                        var FavorHistory1 = (favTeamStats == null) ? 0 : (double)favTeamStats.Wins.ZeroIfNull() / FavorHistoryGames.OneIfZero();
                        var FavorHistoryX = (favTeamStats == null) ? 0 : (double)favTeamStats.Draws.ZeroIfNull() / FavorHistoryGames.OneIfZero();
                        var FavorHistory2 = (favTeamStats == null) ? 0 : (double)favTeamStats.Losses.ZeroIfNull() / FavorHistoryGames.OneIfZero();


                        var HomeGoals = prevGamesScores.Where(x => x.HomeTeam == item.HOME && x.AwayTeam == item.AWAY
                                            && x.MatchDate == item.MATCHDATE.Value && x.Country == item.COUNTRY).FirstOrDefault()?.HOME_GOALS;

                        var AwayGoals = prevGamesScores.Where(x => x.HomeTeam == item.HOME && x.AwayTeam == item.AWAY
                                                                   && x.MatchDate == item.MATCHDATE.Value && x.Country == item.COUNTRY).FirstOrDefault()?.AWAY_GOALS;

                        results.Add(new SummaryModel
                        {
                            COUNTRY = item.COUNTRY,
                            LEAGUE = item.LEAGUE,
                            MatchDate = item.MATCHDATE.Value,
                            HOME = item.HOME,
                            AWAY = item.AWAY,
                            BOOK1 = item.BOOK1,
                            BOOKX = item.BOOKX,
                            BOOK2 = item.BOOK2,
                            NLT1 = item.NLT1,
                            NLTX = item.NLTX,
                            NLT2 = item.NLT2,
                            CLB1B2ChGames = CLB1B2ChGames,
                            CLB1B2Ch1 = (decimal)CLB1B2Ch1.RoundToDecimalPlaces(2),
                            CLB1B2ChX = (decimal)CLB1B2ChX.RoundToDecimalPlaces(2),
                            CLB1B2Ch2 = (decimal)CLB1B2Ch2.RoundToDecimalPlaces(2),
                            B1B2ChGames = B1B2ChGames,
                            B1B2Ch1 = (decimal)B1B2Ch1.RoundToDecimalPlaces(2),
                            B1B2ChX = (decimal)B1B2ChX.RoundToDecimalPlaces(2),
                            B1B2Ch2 = (decimal)B1B2Ch2.RoundToDecimalPlaces(2),
                            CLB1B2Games = CLB1B2Games,
                            CLB1B21 = (decimal)CLB1B21.RoundToDecimalPlaces(2),
                            CLB1B2X = (decimal)CLB1B2X.RoundToDecimalPlaces(2),
                            CLB1B22 = (decimal)CLB1B22.RoundToDecimalPlaces(2),
                            FavorHistoryGames = FavorHistoryGames,
                            FavorHistory1 = (decimal)FavorHistory1.RoundToDecimalPlaces(2),
                            FavorHistoryX = (decimal)FavorHistoryX.RoundToDecimalPlaces(2),
                            FavorHistory2 = (decimal)(FavorHistory2).RoundToDecimalPlaces(2),
                            HomeGoals = HomeGoals,
                            AwayGoals = AwayGoals
                        });

                    }

                    db.Close();
                }
                Cursor.Current = Cursors.Default;
                ExportXLXReport(results);
            }
            catch (Exception ex)
            {

                throw ex;
            }
        }

        private void ExportXLXReport(List<SummaryModel> results)
        {

            // Set GemBox.Spreadsheet license key if you have one
            SpreadsheetInfo.SetLicense("SN-2020Nov05-kLAsb7p1RhzuOPcbbrldwaJd9G5LLgmzIVrtz3GBFIHki2HUw9NYiXGl1LAjxGxXBtms1K4YbATi8Ik0PYPi/Cl6PRw==A");
            // get the file path template\tempQueria.xlsx from curent directory
            var tempfile = Directory.GetCurrentDirectory() + @"\templates\tempQueria2.xlsx";
            // Load Excel file
            var workbook = ExcelFile.Load(tempfile);

            // Select the first worksheet
            var ws = workbook.Worksheets[0];
            var row = 3;
            foreach (var item in results)
            {
                ws.Cells[$"A{row.ToString()}"].Value = item.COUNTRY;
                ws.Cells[$"B{row.ToString()}"].Value = item.LEAGUE;
                ws.Cells[$"C{row.ToString()}"].Value = item.MatchDate.ToString("g");
                ws.Cells[$"D{row.ToString()}"].Value = item.HOME;
                ws.Cells[$"E{row.ToString()}"].Value = item.AWAY;
                ws.Cells[$"F{row.ToString()}"].Value = item.BOOK1.Value;
                ws.Cells[$"G{row.ToString()}"].Value = item.BOOKX.Value;
                ws.Cells[$"H{row.ToString()}"].Value = item.BOOK2.Value;
                ws.Cells[$"I{row.ToString()}"].Value = item.NLT1.RoundToDecimalPlaces(2);
                ws.Cells[$"J{row.ToString()}"].Value = item.NLTX.RoundToDecimalPlaces(2);
                ws.Cells[$"K{row.ToString()}"].Value = item.NLT2.RoundToDecimalPlaces(2);
                ws.Cells[$"M{row.ToString()}"].Value = item.CLB1B2ChGames;
                ws.Cells[$"N{row.ToString()}"].Value = item.CLB1B2Ch1;
                ws.Cells[$"O{row.ToString()}"].Value = item.CLB1B2ChX;
                ws.Cells[$"P{row.ToString()}"].Value = item.CLB1B2Ch2;
                ws.Cells[$"Q{row.ToString()}"].Value = item.B1B2ChGames;
                ws.Cells[$"R{row.ToString()}"].Value = item.B1B2Ch1;
                ws.Cells[$"S{row.ToString()}"].Value = item.B1B2ChX;
                ws.Cells[$"T{row.ToString()}"].Value = item.B1B2Ch2;
                //ws.Cells[$"U{row.ToString()}"].Value = item.CLB1B2Games;
                //ws.Cells[$"G{row.ToString()}"].Value = item.CLB1B21;
                //ws.Cells[$"W{row.ToString()}"].Value = item.CLB1B2X;
                //ws.Cells[$"X{row.ToString()}"].Value = item.CLB1B22;
                ws.Cells[$"U{row.ToString()}"].Value = item.FavorHistoryGames;
                ws.Cells[$"V{row.ToString()}"].Value = item.FavorHistory1;
                ws.Cells[$"W{row.ToString()}"].Value = item.FavorHistoryX;
                ws.Cells[$"X{row.ToString()}"].Value = item.FavorHistory2;
                ws.Cells[$"Y{row.ToString()}"].Value = item.HomeGoals;
                ws.Cells[$"Z{row.ToString()}"].Value = item.AwayGoals;
                row++;
            }
            string saveFileName = Directory.GetCurrentDirectory() + $@"\next.{DateTime.Now.Day.ToString()}.{DateTime.Now.Month.ToString()}.{DateTime.Now.Year.ToString()}.xlsx";
            if (File.Exists(saveFileName)) File.Delete(saveFileName);
            workbook.Save(saveFileName);
            Process.Start(new ProcessStartInfo(saveFileName) { UseShellExecute = true });
        }

        private async Task GetQueriesAsynch()
        {
            using (IDbConnection db = new SqlConnection(_netGoalToolsConStr))
            {
                var q = await db.QueryAsync<Query>("SELECT [ID],[Name],[Country],[League],[HomeTeam],[AwayTeam],[B1NL1NF1NN1],[B2NL2BF2NN2],[Chart],[UseTime],[DU1],[DU2],[DUX] FROM [neuralnetGoalData].[dbo].[Queries]", commandType: CommandType.Text, commandTimeout: 300);
                Queries = q.ToList();
            }

        }

        private async Task<IEnumerable<SummaryStatsModel>> GetSummaryStatsAsynch()
        {
            using (IDbConnection db = new SqlConnection(_netGoalToolsConStr))
            {
                var q = await db.QueryAsync<SummaryStatsModel>("select * from [dbo].[FN_SUPER_SUMMARY_STATS]()", commandType: CommandType.Text, commandTimeout: 300);
                return q.ToList();
            }
        }

        private async Task<IEnumerable<SoccerTeamBudget>> GetBudgets()
        {

            using (IDbConnection db = new SqlConnection(_netGoalToolsConStr))
            {
                var q = await db.QueryAsync<SoccerTeamBudget>("select * from [dbo].[SoccerTeamBudgets]", commandType: CommandType.Text, commandTimeout: 300);
                return q.ToList();
            }

        }
 
        private async Task<List<ExtdQueryResult>> ExecuteSavedQueriesAsynch()
        {
            try
            {
                var allQryResults = new List<ExtdQueryResult>();
                await GetQueriesAsynch();

                foreach (var query in Queries)
                {
                    var qResult = await GetQueryResults(query);

                    allQryResults.Add(new ExtdQueryResult()
                    {
                        QName = query.Name,
                        QueryResults = qResult
                    });

                }
                return allQryResults;
            }
            catch (Exception ex)
            {

                throw ex;
            }
        
        }

        private IOverlaySplashScreenHandle ShowProgressPanel()
        {
            IOverlaySplashScreenHandle handle = SplashScreenManager.ShowOverlayForm(this);
            return handle;
        }

        private void CloseProgressPanel(IOverlaySplashScreenHandle handle)
        {
            if (handle != null)
                SplashScreenManager.CloseOverlayForm(handle);
        }

        private async void btnSuperSummary_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            try
            {
                var handle = ShowProgressPanel();
                // Execute asynchronous operations concurrently
                var queriesTask = ExecuteSavedQueriesAsynch();
                var favoriteTeamStatsTask = getFavoriteTeamStatisticsAsynch();
                var sumStatsTask = GetSummaryStatsAsynch();

                // Await completion of all tasks
                await Task.WhenAll(queriesTask, favoriteTeamStatsTask, sumStatsTask);

                // Retrieve results from completed tasks
                var queriesResults = await queriesTask;
                var favoriteTeamStats = await favoriteTeamStatsTask;
                var sumStats = await sumStatsTask;
                var budget = await GetBudgets();

                CloseProgressPanel(handle);

                ExportSuperSummaryReport(sumStats.ToList(), favoriteTeamStats, queriesResults,budget.ToList());

            }
            catch (Exception ex)
            {

                throw ex;
            }                 
        }

        private async void ExportSuperSummaryReport(List<SummaryStatsModel> summaries, List<FavoriteTeamStatisticsAll> favStats, List<ExtdQueryResult> queryResults, List<SoccerTeamBudget> budgets)
        {
            // Set GemBox.Spreadsheet license key if you have one
            SpreadsheetInfo.SetLicense("SN-2020Nov05-kLAsb7p1RhzuOPcbbrldwaJd9G5LLgmzIVrtz3GBFIHki2HUw9NYiXGl1LAjxGxXBtms1K4YbATi8Ik0PYPi/Cl6PRw==A");
            // get the file path template\tempQueria.xlsx from curent directory
            var tempfile = Directory.GetCurrentDirectory() + @"\templates\BigSummaryReportTemplate.xlsx";
            // Load Excel file
            var workbook = ExcelFile.Load(tempfile);

            // Select the first worksheet
            var ws = workbook.Worksheets["sum rep"];
            var template = workbook.Worksheets["template"];

            int currentRow = 3; // Assuming row 1 is for headers

            //var qCellRangeBuffer = new List<CellRange>();
            
            var col = 39;

            foreach (var query in queryResults)
            {
                template.Cells[$"B1"].Value = query.QName;
                 currentRow = 3;
                foreach (var result in query.QueryResults.OrderBy(x=> x.MATCHDATE).ThenBy(y=>y.COUNTRY).ThenBy(x=> x.LEAGUE).ThenBy(x=> x.HOME).ThenBy(x=>x.AWAY))
                {
                    Int64 TGF = result.Matches.Count;
                    
                    decimal HWINS = 0;
                    decimal DRAWS = 0;
                    decimal AWINS = 0;
                    decimal UNDER = 0;
                    decimal OVER = 0;
                    decimal GG = 0;
                    decimal NG = 0;

                    if (TGF > 0)
                    {
                        HWINS = Math.Round((decimal)result.HWINS.Value/ (decimal)TGF * 100,0);
                        //DRAWS = Math.Round((decimal)result.DRAWS.Value / (decimal)TGF * 100, 0);
                        AWINS = Math.Round((decimal)result.AWINS.Value / (decimal)TGF * 100, 0);
                        DRAWS = 100 - (HWINS + AWINS);
                        UNDER = Math.Round((decimal)result.UNDER.Value / (decimal)TGF * 100, 0);
                        OVER = Math.Round((decimal)result.OVER.Value / (decimal)TGF * 100, 0);
                        GG = Math.Round((decimal)result.GG.Value / (decimal)TGF * 100, 0);
                        NG = Math.Round((decimal)result.NG.Value / (decimal)TGF * 100, 0);
                        
                    }

                    template.Cells[$"B{currentRow}"].Value = TGF;
                    template.Cells[$"C{currentRow}"].Value = HWINS;
                    template.Cells[$"D{currentRow}"].Value = DRAWS;
                    template.Cells[$"E{currentRow}"].Value = AWINS;
                    template.Cells[$"F{currentRow}"].Value = UNDER;
                    template.Cells[$"G{currentRow}"].Value = OVER;
                    template.Cells[$"H{currentRow}"].Value = GG;
                    template.Cells[$"I{currentRow}"].Value = NG;
                    //template.Cells[$"J{currentRow}"].Value = result.COUNTRY;
                    //template.Cells[$"K{currentRow}"].Value = result.LEAGUE;
                    //template.Cells[$"L{currentRow}"].Value = result.HOME;
                    //template.Cells[$"M{currentRow}"].Value = result.AWAY;

                    

                    currentRow++;
                    
                }
                template.Cells.GetSubrangeAbsolute(0, 0, currentRow, 9).CopyTo(ws, 0, col);
                col += 9;
                
            }

            currentRow = 2;

            foreach (var summary in summaries.OrderBy(x => x.MatchDate).ThenBy(y => y.Country).ThenBy(x => x.League).ThenBy(x => x.Home).ThenBy(x => x.Away))
            {
                // Assuming you have a way to identify corresponding entries
                var favStat = favStats.FirstOrDefault(f => f.MATCHDATE == summary.MatchDate && f.HOME == summary.Home && f.AWAY == summary.Away);

                decimal FavorHistory1 = 0;
                decimal FavorHistory2 = 0;
                decimal FavorHistoryX = 0;

                if (favStat?.Matches != 0)
                {
                    FavorHistory1 = (decimal)favStat?.Wins / (decimal)favStat?.Matches;
                    FavorHistory2 = (decimal)favStat?.Losses / (decimal)favStat?.Matches;
                    FavorHistoryX = (decimal)favStat?.Draws / (decimal)favStat?.Matches;
                }
                var htv = budgets.FirstOrDefault(x => x.TeamName == summary.Home)?.PlayersValue;
                var atv = budgets.FirstOrDefault(x => x.TeamName == summary.Away)?.PlayersValue;

                var hts = budgets.FirstOrDefault(x => x.TeamName == summary.Home)?.Salaries;
                var ats = budgets.FirstOrDefault(x => x.TeamName == summary.Away)?.Salaries;

                ws.Cells[currentRow, 0].Value = summary.Country;
                ws.Cells[currentRow, 1].Value = summary.League;
                ws.Cells[currentRow, 2].Value = summary.MatchDate;
                ws.Cells[currentRow, 3].Value = summary.Home;
                ws.Cells[currentRow, 4].Value = summary.Away;
                ws.Cells[currentRow, 5].Value = summary.Book1;
                ws.Cells[currentRow, 6].Value = summary.BookX;
                ws.Cells[currentRow, 7].Value = summary.Book2;
                ws.Cells[currentRow, 8].Value = summary.NLT1;
                ws.Cells[currentRow, 9].Value = summary.NLTX;
                ws.Cells[currentRow, 10].Value = summary.NLT2;
                ws.Cells[currentRow, 12].Value = summary.HomeRank;
                ws.Cells[currentRow, 13].Value = summary.AwayRank;
                ws.Cells[currentRow, 15].Value = summary.HomeLast4Goals;
                ws.Cells[currentRow, 16].Value = summary.AwayLast4Goals;
                
                ws.Cells[currentRow, 18].Value = htv;
                ws.Cells[currentRow, 19].Value = hts;
                ws.Cells[currentRow, 21].Value = atv;
                ws.Cells[currentRow, 22].Value = ats;


                ws.Cells[currentRow, 24].Value = favStat?.Matches;
                ws.Cells[currentRow, 25].Value = FavorHistory1;
                ws.Cells[currentRow, 26].Value = FavorHistoryX;
                ws.Cells[currentRow, 27].Value = FavorHistory2;
                ws.Cells[currentRow, 28].Value = summary.CI;
                ws.Cells[currentRow, 29].Value = summary.CJ;
                ws.Cells[currentRow, 30].Value = summary.CK;
                ws.Cells[currentRow, 31].Value = summary.B1;
                ws.Cells[currentRow, 32].Value = summary.NL1;
                ws.Cells[currentRow, 33].Value = summary.NF1;
                ws.Cells[currentRow, 34].Value = summary.NN1;
                ws.Cells[currentRow, 35].Value = summary.B2;
                ws.Cells[currentRow, 36].Value = summary.NL2;
                ws.Cells[currentRow, 37].Value = summary.NF2;
                ws.Cells[currentRow, 38].Value = summary.NN2;
                ws.Cells[currentRow, 39].Value = summary.TREND;


                currentRow++;
            }
            
            workbook.Worksheets.Remove("template");

            string saveFileName = Directory.GetCurrentDirectory() + $@"\super.summary.next.{DateTime.Now.Day.ToString()}.{DateTime.Now.Month.ToString()}.{DateTime.Now.Year.ToString()}.xlsx";
            if (File.Exists(saveFileName)) File.Delete(saveFileName);
            workbook.Save(saveFileName);
            Process.Start(new ProcessStartInfo(saveFileName) { UseShellExecute = true });

        }

    }
}
