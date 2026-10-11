using Dapper;
using FastMember;
using GemBox.Spreadsheet;
using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Common;
using oddsmiser.scraper.betexplorer.DAL;
using oddsmiser.scraper.betexplorer.Enums;
using oddsmiser.scraper.betexplorer.Models;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace oddsmiser.scraper.betexplorer
{

    public class SoccerHistorical : ISoccerHistorical
    {

        private readonly ILogger<SoccerHistorical> _loger;
        private readonly betexplorerContext _context;
        private readonly string _constr;
        private readonly IParser _parser;
        public readonly string baseULR = "https://www.betexplorer.com";
        static readonly Stopwatch timer = new Stopwatch();

        //private readonly IWebDriver driver;

        public SoccerHistorical(ILogger<SoccerHistorical> logger,betexplorerContext context, IParser parser)
        {
            this._loger = logger;
            this._context = context;
            this._constr = _context.Database.GetConnectionString();
            _parser = parser;   
        }
        public async Task<bool> GetHistoricalDataAsynch()
        {
            return await GetHistoricalDataSeq(null,null,null);
        }

        public async Task<bool> GetHistoricalDataAsynch(string Country)
        {
            return await GetHistoricalDataSeq(Country,null,null);
        }

        public async Task<bool> GetHistoricalDataAsynch(string Country, string Division)
        {
            return await GetHistoricalDataSeq(Country, Division, null);
        }

        public async Task<bool> GetHistoricalDataAsynch(string Country, string Division, string Season)
        {
             return await GetHistoricalDataSeq(Country, Division, Season);
        }

        private async Task<bool> GetHistoricalDataSeq(string Country, string Division, string Season)
        {
            var hasErrors = false;
            var q = _context.CountryLeagues.Where(x => x.IsActive == true);

            if (Country != null)
                q = q.Where(x => x.Country == Country);

            if (Season != null)
                q = q.Where(x => x.Season == Season);

            if (Division != null)
                q = q.Where(q => q.Division == Division);


            var countryLeagueLinks = await q.ToListAsync();

            foreach (var link in countryLeagueLinks)
            {
                var result = await _parser.GetCountryLeagueResultsAsynch(link.Country, link.Season, link.Division, link.URL);

                if( result.soccerResults.Count>0) _context.SoccerResults.AddRange(result.soccerResults);
                if (result.errors.Count > 0) {
                    _context.Errors.AddRange(result.errors);
                    hasErrors = true;
                }
                

                await _context.SaveChangesAsync();
            }

            return hasErrors;

        }

        private async Task<bool> GetHistoricalDataParallel(string Country, string Division, string Season)
        {
            ConcurrentBag<Errors> errors = new ConcurrentBag<Errors>();
            ConcurrentBag<SoccerResult> soccerResults = new ConcurrentBag<SoccerResult>();

            var q = _context.CountryLeagues.Where(x => x.IsActive == true);

            if(Country != null)
                 q = q.Where(x => x.Country == Country);

            if (Season != null)
                q=q.Where(x => x.Season == Season);
           
            if(Division != null)
                q=q.Where(q => q.Division == Division);


            var countryLeagueLinks = await q.ToListAsync();


            SemaphoreSlim semaphore = new SemaphoreSlim(10);

            var tasks = countryLeagueLinks.Select(async link =>
            {
                await semaphore.WaitAsync();

                try
                {
                    var result = await _parser.GetCountryLeagueResultsAsynch(link.Country, link.Season, link.Division, link.URL);

                    foreach (var item in result.soccerResults)
                    {
                        soccerResults.Add(item);
                    }

                    foreach (var item in result.errors)
                    { 
                        errors.Add(item);
                    }
                    

                }
                finally
                {
                    semaphore.Release();
                }
            });

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            if(errors.Count > 0)
                await _context.Errors.AddRangeAsync(errors);
            if (soccerResults.Count > 0)
                await _context.SoccerResults.AddRangeAsync(soccerResults);

            await _context.SaveChangesAsync();

            return errors.Count>0?false:true;
        }


        public async Task<bool> BuildLeagueTablesHistory()
        {
           
            IEnumerable<SoccerCountryLeagues> countryLeagueLinks = null;
            using (var connection = new SqlConnection(_constr))
            {
                var sql = "select distinct 0 as id,Country,Season,Division, null as URL,null as IsActive from SoccerResults";
                countryLeagueLinks = await connection.QueryAsync<SoccerCountryLeagues>(sql);
            }
            //using (var connection = new SqlConnection(_constr))
            //{
            //    foreach (var country in countryLeagueLinks)
            //    {

            //    }
            //}
            return await BuildSelectedLeagueTables(countryLeagueLinks.ToList());
        }


        public async Task<bool> BuildLeagueTables()
        {
           
            var countryLeagueLinks = await  _context.SoccerRunningLeagues.Select(x=> new SoccerCountryLeagues() { Country = x.Country, Division = x.Division, Season = x.Season, URL=x.URL }).ToListAsync();

            

            return await BuildSelectedLeagueTables(countryLeagueLinks);

        }

        private async Task<bool> BuildSelectedLeagueTables(List<SoccerCountryLeagues> countryLeagueLinks)
        {
            using (var connection = new SqlConnection(_constr))
            {

                SemaphoreSlim semaphore = new SemaphoreSlim(1);

                var tasks = countryLeagueLinks.Select(async link =>
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        _loger.LogInformation($"Building League Table for Country: {link.Country}, Season: {link.Season} League: {link.Division}");

                        var vector = await connection.ExecuteAsync($"[dbo].[Build_Soccer_league_Tables] '{link.Country}', '{link.Season}', '{link.Division}'", commandTimeout: 200);

                        // await  _context.Database.ExecuteSqlInterpolatedAsync($"[dbo].[Build_Soccer_league_Tables] {link.Country}, {link.Season}, {link.Division}");

                        _loger.LogInformation($"Finished League Table for Country: {link.Country}, Season: {link.Season} League: {link.Division}");

                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                // Wait for all tasks to complete
                await Task.WhenAll(tasks);

            }

            return true;
        }

        public async Task GetLatestResultsAsynch()
        {
            _loger.LogInformation($"Getting Latest Results.");
            var runningLeagues = _context.SoccerRunningLeagues.ToList();

            ConcurrentBag<Errors> errors = new ConcurrentBag<Errors>();
            ConcurrentBag<SoccerResult> soccerResults = new ConcurrentBag<SoccerResult>();


            SemaphoreSlim semaphore = new SemaphoreSlim(10);

            var tasks = runningLeagues.Select(async link =>
            {
                await semaphore.WaitAsync();

                try
                {
                    var result = await _parser.GetCountryLeagueResultsAsynch(link.Country, link.Season, link.Division, link.URL);

                    foreach (var item in result.soccerResults)
                    {
                        soccerResults.Add(item);
                    }

                    foreach (var item in result.errors)
                    {
                        errors.Add(item);
                    }


                }
                finally
                {
                    semaphore.Release();
                }
            });

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            _loger.LogInformation($"Upload data to host started.");
            timer.Start();
            using (var connection = new SqlConnection(_constr))
            {
                string sqlQuery = @" IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SoccerResultsTmp]') AND type in (N'U'))
                                     DROP TABLE [dbo].[SoccerResultsTmp];

                                    CREATE TABLE [dbo].[SoccerResultsTmp]([Id] [int] NOT NULL,[Country] [nvarchar](250) NULL,[Division] [nvarchar](250) NULL,
                                    [Season] [nvarchar](250) NULL,[MatchDate] [datetime2](7) NOT NULL,[HomeTeam] [nvarchar](250) NULL,[AwayTeam] [nvarchar](250) NULL,
                                    [HomeScore] [int] NOT NULL,[AwayScore] [int] NOT NULL,[HalfTimeHomeScore] [int] NOT NULL,[HalfTimeAwayScore] [int] NOT NULL,
                                    [AvgHomeOdds] [decimal](18, 2) NOT NULL, [AvgDrawOdds] [decimal](18, 2) NOT NULL,[AvgAwayOdds] [decimal](18, 2) NOT NULL,[Round] [nvarchar](max) NULL)";
                connection.Execute(sqlQuery);

                string sqlQueryInsert = @"INSERT INTO [dbo].[SoccerResultsTmp] VALUES (@Id, @Country, @Division, @Season, @MatchDate, @HomeTeam, @AwayTeam, 
                @HomeScore, @AwayScore, @HalfTimeHomeScore, @HalfTimeAwayScore, @AvgHomeOdds, @AvgDrawOdds, @AvgAwayOdds, @Round)";

                var copyParameters = new[]
                    {
                        nameof(SoccerResult.Id),
                        nameof(SoccerResult.Country),
                        nameof(SoccerResult.Division),
                        nameof(SoccerResult.Season),
                        nameof(SoccerResult.MatchDate),
                        nameof(SoccerResult.HomeTeam),
                        nameof(SoccerResult.AwayTeam),
                        nameof(SoccerResult.HomeScore),
                        nameof(SoccerResult.AwayScore),
                        nameof(SoccerResult.HalfTimeHomeScore),
                        nameof(SoccerResult.HalfTimeAwayScore),
                        nameof(SoccerResult.AvgHomeOdds),
                        nameof(SoccerResult.AvgDrawOdds),
                        nameof(SoccerResult.AvgAwayOdds),
                        nameof(SoccerResult.Round)
                    };

                using (var sqlCopy = new SqlBulkCopy(_constr))
                {
                    sqlCopy.DestinationTableName = "[SoccerResultsTmp]";
                    sqlCopy.BatchSize = 500;
                    using (var reader = ObjectReader.Create(soccerResults, copyParameters))
                    {
                        sqlCopy.WriteToServer(reader);
                    }
                }

                //connection.Execute(sqlQueryInsert, soccerResults);



                string sqlQueryInsertNewResults = @" INSERT INTO [dbo].[SoccerResults] 
                                     SELECT [Country],[Division],[Season],[MatchDate],[HomeTeam],[AwayTeam],[HomeScore],[AwayScore],
                                     [HalfTimeHomeScore],[HalfTimeAwayScore],[AvgHomeOdds],[AvgDrawOdds],[AvgAwayOdds],[Round]
                                     FROM [dbo].[SoccerResultsTmp] tmp  WHERE NOT EXISTS ( SELECT 1 FROM [dbo].[SoccerResults] res
                                     WHERE res.MatchDate = tmp.MatchDate AND 
                                           res.HomeTeam = tmp.HomeTeam AND
                                           res.AwayTeam = tmp.AwayTeam AND
                                           res.Country = tmp.Country AND
                                           res.Division = tmp.Division AND
                                           res.Season = tmp.Season)";

                connection.Execute(sqlQueryInsertNewResults);

                


            }
            timer.Stop();
            _loger.LogInformation($"Upload data to host finished. Execution time ->{timer.Elapsed.ToString()}");
                //foreach (var result in soccerResults)
                //{
                //    var exists = await _context.SoccerResults.Where(x => x.Division == result.Division && x.MatchDate == result.MatchDate && x.HomeTeam == result.HomeTeam && x.AwayTeam == result.AwayTeam && x.Country == result.Country && x.Season == result.Season).FirstOrDefaultAsync();
                //    if (exists == null)
                //    {
                //       await _context.SoccerResults.AddAsync(result);
                //    }
                //    else
                //    {
                //        if(result.AvgAwayOdds > 0)
                //            exists.AvgAwayOdds = result.AvgAwayOdds;
                //        if(result.AvgHomeOdds>0)
                //            exists.AvgHomeOdds = result.AvgHomeOdds;
                //         if (result.AvgDrawOdds > 0)
                //            exists.AvgDrawOdds = result.AvgDrawOdds;
                //    }

                //}

            foreach (var item in errors)
            {
                await _context.Errors.AddAsync(item);
            }

            await _context.SaveChangesAsync();

            _loger.LogInformation($"Finished Latest Results.");
        }

        public async Task Fixtures()
        {
            _loger.LogInformation($"Import fixtures process started.");

            var runningLeagues = await _context.SoccerRunningLeagues.ToListAsync();

            await _context.Database.ExecuteSqlInterpolatedAsync($"truncate table [dbo].[SoccerFixtures]");


            ConcurrentBag<SoccerFixture> fixtures = new ConcurrentBag<SoccerFixture>();

            SemaphoreSlim semaphore = new SemaphoreSlim(10);

            var tasks = runningLeagues.Select(async link =>
            {
                await semaphore.WaitAsync();

                try
                {
                    var result = await _parser.GetCountryLeagueFixtures(link.Country, link.Season, link.Division, link.URL);

                    foreach (var item in result)
                    {
                        fixtures.Add(item);
                    }


                }
                finally
                {
                    semaphore.Release();
                }
            });

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);


            foreach (var item in fixtures)
            {

                await _context.SoccerFixtures.AddAsync(item);

            }

            await _context.SaveChangesAsync();

            using (var connection = new SqlConnection(_constr))
            {
                var vector = await connection.QueryAsync<SoccerDataVector>("select * from dbo.fn_Soccer_fixtures()", commandTimeout: 200);
                var path = ExportFixtures(vector.ToList());
                OpenFile(path);
            }
            
            _loger.LogInformation($"Import fixtures process finished.");
            
        }

        

        public async Task InitCountryLeagueLinksTable()
        {

            _loger.LogInformation("Cleaning League Links Table");

            await _context.Database.ExecuteSqlRawAsync("truncate table [dbo].[CountryLeagues]");

            var countries = await GetCountryLinks();

            _loger.LogInformation("Initializing Data");

            // SemaphoreSlim to limit number of concurrent tasks
            SemaphoreSlim semaphore = new SemaphoreSlim(10);

            var tasks = countries.Select(async link =>
            {
                await semaphore.WaitAsync();

                try
                {
                    _loger.LogInformation("Looping through Countries");
                    Console.WriteLine($"Country Name {link.Key}-> Link {link.Value}");

                    var leagues = await  _parser.GetCountryLeagues(this.baseULR + link.Value, link.Key);

                    _context.CountryLeagues.AddRange(leagues);
                    await _context.SaveChangesAsync();
                }
                finally
                {
                    semaphore.Release();
                }
            });

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);


            _loger.LogInformation("League Links Table created");
        }

        private async Task< Dictionary<string,string>> GetCountryLinks()
        {
            Dictionary<string,string> vs = new Dictionary<string,string>();
            try
            {
                _loger.LogInformation("Getting Country Links");

                //driver.Navigate().GoToUrl(baseULR);
                //var html = driver.PageSource;
                HtmlWeb web = new HtmlWeb();
                HtmlDocument document = await web.LoadFromWebAsync(baseULR + "/soccer/");
                var xpath_countries = @"//ul[@id='countries-select']/li";
                var links = document.DocumentNode.SelectNodes(xpath_countries); //doc.DocumentNode.SelectNodes(@"//*[@id=""" + @"""" + @"countries-select" + @"""" + "]");
                
                foreach (var link in links)
                {
                    var a = link.ChildNodes[0].ChildNodes.Where(x => x.HasClass("list-events__item__title")).FirstOrDefault();
                    var countryName = a.InnerText;
                    var countryLink = a.Attributes["href"].Value;
                    vs.Add(countryName,countryLink);
                }
              
            }
            catch (System.Exception ex)
            {

                _loger.LogError(ex.Message);
                
            }
            return vs;
        }

        

       
        public async Task<List<SoccerLeagueTable>> BuildLeagueTable(string country,string season,string league)
        {
            var result = new List<SoccerLeagueTable>();
            try
            {
                var StartOfSeason = await _context.SoccerResults.Where(x => x.Division == league && x.Country == country && x.Season == season).MinAsync(y => y.MatchDate);
                var EndOfSeason = await _context.SoccerResults.Where(x => x.Division == league && x.Country == country && x.Season == season).MaxAsync(y => y.MatchDate);

                var matches = await _context.SoccerResults.Where(x => x.Division == league && x.Country == country && x.Season == season && x.MatchDate>=StartOfSeason && x.MatchDate <= EndOfSeason).ToListAsync();

                var teams = matches.OrderBy(y=>y.HomeScore).Select(x => x.HomeTeam).Distinct().ToList();

                foreach (var gameGroup in matches.GroupBy(x=>x.MatchDate))
                {
                    var matchDate = gameGroup.Key;

                    foreach (var team in teams)
                    {
                           
                       
                            var total_matches_played = matches.Where(x => (x.HomeTeam == team || x.AwayTeam == team) && x.MatchDate < matchDate).Count();

                            var home_matches_played = matches.Where(x => x.HomeTeam == team && x.MatchDate < matchDate).Count(); 

                            var away_matches_played = matches.Where(x => x.HomeTeam == team && x.MatchDate <= matchDate).Count();  

                            var home_matches_won = matches.Where(x => x.HomeTeam == team && x.MatchDate <= matchDate && x.HomeScore>x.AwayScore).Count(); 

                            var away_matches_won = matches.Where(x => x.AwayTeam == team && x.MatchDate <= matchDate && x.HomeScore < x.AwayScore).Count();

                            var total_matches_won = home_matches_won + away_matches_won;

                            var home_matches_drawn = matches.Where(x => x.HomeTeam == team && x.MatchDate <= matchDate && x.HomeScore == x.AwayScore).Count(); 

                            var away_matches_drawn = matches.Where(x => x.AwayTeam == team && x.MatchDate <= matchDate && x.HomeScore == x.AwayScore).Count(); 

                            var total_matches_drawn = home_matches_drawn + away_matches_drawn;

                            var home_matches_lost = matches.Where(x => x.HomeTeam == team && x.MatchDate <= matchDate && x.HomeScore < x.AwayScore).Count(); 

                            var away_matches_lost = matches.Where(x => x.AwayTeam == team && x.MatchDate <= matchDate && x.HomeScore > x.AwayScore).Count();

                            var total_matches_lost = home_matches_lost + away_matches_lost;


                            var home_matches_goals_scored = matches.Where(x => x.HomeTeam == team && x.MatchDate < matchDate).Sum(y=>y.HomeScore); 

                            var home_matches_goals_against = matches.Where(x => x.HomeTeam == team && x.MatchDate < matchDate).Sum(y => y.AwayScore); 


                            var away_matches_goals_scored = matches.Where(x => x.AwayTeam == team && x.MatchDate < matchDate).Sum(y => y.AwayScore); 

                            var away_matches_goals_against = matches.Where(x => x.AwayTeam == team && x.MatchDate < matchDate).Sum(y => y.HomeScore); 

                            var total_goals_for = home_matches_goals_scored + away_matches_goals_scored;

                            var total_goals_against = home_matches_goals_against + away_matches_goals_against;

                            var total_goal_difference = total_goals_for - total_goals_against;

                            var id = teams.IndexOf(team);

                            var points = total_matches_won * 3 + total_matches_drawn * 1 + total_goal_difference * 0.00001 + total_goal_difference * 0.0000001 - ((int)Convert.ToChar(team.Substring(0, 1)) / 100 * 0.00000001) - (id / 100 * 0.000000001);

                            result.Add(new SoccerLeagueTable() { Away_Goals_Against = away_matches_goals_against, Away_Goals_For = away_matches_goals_scored, 
                                Away_Goal_Difference = away_matches_goals_scored - away_matches_goals_against,Away_Matches_Drawn = away_matches_drawn, Away_Matches_Lost = away_matches_lost, 
                                Away_Matches_Played = away_matches_played, Away_Matches_Won = away_matches_won, Country = country, Division = league, Goals_Against = total_goals_against,
                                Goals_For = total_goals_for, Goal_Difference = total_goal_difference, Home_Goals_Against = home_matches_goals_against, Home_Goals_For = home_matches_goals_scored, 
                                Home_Goal_Difference = home_matches_goals_scored - home_matches_goals_against, Home_Matches_Drawn = home_matches_drawn, Home_Matches_Lost = home_matches_lost, 
                                Home_Matches_Played = home_matches_played, Home_Matches_Won = home_matches_won, MatchDate = matchDate, Matches_Drawn = total_matches_drawn,
                                Matches_Lost = total_matches_lost, Matches_Played = total_matches_played, Matches_Won = total_matches_won, Points = (decimal)points, POS = id });

                        
                    } // teams loop


                    //rank
                    var rnk = 1;
                    foreach (var item in result.Where(y=>y.MatchDate == matchDate).OrderByDescending(x=>x.Points))
                    {
                        item.Ranking = rnk;

                        rnk++;
                    }

                }// match date loop


            }
            catch (Exception ex)
            {

                _loger.LogError(ex.Message);
            }



            return result;
        }

        public async Task<bool>ExportAsynch()
        {
            try
            {
                using (var connection = new SqlConnection(_constr))
                {

                    var currDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                    var templFile = Path.Combine(currDir, "templates", "Training_data.xlsx");
                    SpreadsheetInfo.SetLicense("SN-2020Nov05-kLAsb7p1RhzuOPcbbrldwaJd9G5LLgmzIVrtz3GBFIHki2HUw9NYiXGl1LAjxGxXBtms1K4YbATi8Ik0PYPi/Cl6PRw==A");
                    var expFileFullName = Path.Combine(currDir, "OddsFreqProbTrainingData" + DateTime.Now.ToString("dd-MM-yyHHmmss") + ".xlsx");
                    var oBook = new ExcelFile();
                    oBook = ExcelFile.Load(templFile);
                    var templateWS = oBook.Worksheets["template"];
                    var vector = await connection.QueryAsync<SoccerDataVector>("SELECT * FROM [dbo].[DataVectors] order by Country,Division,Seazon,MatchDate", commandTimeout: 200);
                    var runningLeagues = await connection.QueryAsync<SoccerRunningLeague>("SELECT * FROM [dbo].[SoccerRunningLeagues]", commandTimeout: 200);

                    foreach (var league in vector.GroupBy(x=> new {x.Country,x.Division }))
                    {
                        var wsName = league.Key.Country + "-" + league.Key.Division;
                        var runningSeason = runningLeagues.Where(x=>x.Division == league.Key.Division && x.Country == league.Key.Country).FirstOrDefault();
                        if (runningSeason == null) { continue; }
                        var trainingData = league.Where(x => x.Country == league.Key.Country && x.Division == league.Key.Division && x.Seazon != runningSeason.Season);
                        var testData = league.Where(x => x.Country == league.Key.Country && x.Division == league.Key.Division && x.Seazon == runningSeason.Season);

                        var wsTrainingData = oBook.Worksheets.AddCopy(wsName, templateWS);
                        fillWorkSheet(trainingData,ref wsTrainingData);

                        var wsTestData = oBook.Worksheets.AddCopy(wsName + " T", templateWS);
                        fillWorkSheet(testData, ref wsTestData);

                    }

                    oBook.Worksheets.Remove("template");
                    oBook.Save(expFileFullName);

                    oBook = null;
                }


                return true;
            }
            catch (Exception ex)
            {
                _loger.LogError(ex, "Export of training data failed");
                return false;
            }
                       
        }

        private void OpenFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (OperatingSystem.IsWindows())
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                else if (OperatingSystem.IsMacOS())
                    Process.Start("open", new[] { path });
                else
                    Process.Start("xdg-open", new[] { path });
            }
            catch (Exception ex)
            {
                _loger.LogWarning(ex, $"Could not open {path}");
            }
        }

        public string ExportFixtures(List<SoccerDataVector> fixtures)
        {
            try
            {
                SpreadsheetInfo.SetLicense("SN-2020Nov05-kLAsb7p1RhzuOPcbbrldwaJd9G5LLgmzIVrtz3GBFIHki2HUw9NYiXGl1LAjxGxXBtms1K4YbATi8Ik0PYPi/Cl6PRw==A");
                var currDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                var templFile = Path.Combine(currDir, "templates", "Training_data.xlsx");
                
                var downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Directory.CreateDirectory(downloadsDir);
                var expFileFullName = Path.Combine(downloadsDir, "Fixtures" + DateTime.Now.ToString("ddMMyy") + ".xlsx");
                if(File.Exists(expFileFullName))
                    File.Delete(expFileFullName);

                var oBook = new ExcelFile();
                oBook = ExcelFile.Load(templFile);
                var templateWS = oBook.Worksheets["template"];
                
                var wsTrainingData = oBook.Worksheets.AddCopy("Fixtures", templateWS);
                fillWorkSheet(fixtures, ref wsTrainingData);
                
                oBook.Worksheets.Remove("template");
                oBook.Save(expFileFullName);
                oBook = null;

                return expFileFullName;
               
            }
            catch (Exception ex)
            {
                _loger.LogError(ex, "Export of fixtures failed");
                return String.Empty;
            }
        }

        private ExcelWorksheet fillWorkSheet(IEnumerable<SoccerDataVector> data, ref ExcelWorksheet worksheet)
        {
            var row = 1;

            foreach (var match in data)
            {

                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Country].Value = match.Country;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Division].Value = match.Division;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Seazon].Value = match.Seazon;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.MatchDate].Value = match.MatchDate;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HomeTeam].Value = match.HomeTeam;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.AwayTeam].Value = match.AwayTeam;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HomeScore].Value = match.Home_Team_Total_Matches_Played.HasValue && match.Home_Team_Total_Matches_Played .Value > 0? (decimal)(match.Home_Team_Points/match.Home_Team_Total_Matches_Played):0;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.AwayScore].Value = (match.HOME_TEAM_LAST_5_GAMES_WINS*3 + match.HOME_TEAM_LAST_5_GAMES_DRAWS*1)/ 5;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HalfTimeHomeScore].Value = match.Away_Team_Total_Matches_Played.HasValue && match.Away_Team_Total_Matches_Played.Value>0 ?  match.Away_Team_points/match.Away_Team_Total_Matches_Played: 0;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HalfTimeAwayScore].Value = (match.AWAY_TEAM_LAST_5_GAMES_WINS*3 + match.AWAY_TEAM_LAST_5_GAMES_WINS_DRAWS*1)/ 5;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.AvgHomeOdds].Value = match.AvgHomeOdds;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.AvgDrawOdds].Value = match.AvgDrawOdds;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.AvgAwayOdds].Value = match.AvgAwayOdds;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Home_Team_Total_Matches_Played].Value = match.Home_Team_Total_Matches_Played;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Home_Team_Ranking].Value = match.Home_Team_Ranking;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Home_Team_Points].Value = match.Home_Team_Points;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Home_Team_Matches_Played_At_Home_SoFar].Value = match.Home_Team_Matches_Played_At_Home_SoFar;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Home_Team_Matches_Played_At_Home_SoFar_Goals_For].Value = match.Home_Team_Matches_Played_At_Home_SoFar_Goals_For;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Home_Team_Matches_Played_At_Home_SoFar_Goals_Against].Value = match.Home_Team_Matches_Played_At_Home_SoFar_Goals_Against;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Home_Team_Matches_Played_Away_SoFar].Value = match.Home_Team_Matches_Played_Away_SoFar;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Home_Team_Matches_Played_Away_SoFar_Goals_For].Value = match.Home_Team_Matches_Played_Away_SoFar_Goals_For;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Home_Team_Matches_Played_Away_SoFar_Goals_Against].Value = match.Home_Team_Matches_Played_Away_SoFar_Goals_Against;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Away_Team_Total_Matches_Played].Value = match.Away_Team_Total_Matches_Played;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Away_Team_Ranking].Value = match.Away_Team_Ranking;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Away_Team_points].Value = match.Away_Team_points;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Away_Team_Matches_Played_At_Home_SoFar].Value = match.Away_Team_Matches_Played_At_Home_SoFar;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Away_Team_Matches_Played_At_Home_SoFar_Goals_For].Value = match.Away_Team_Matches_Played_At_Home_SoFar_Goals_For;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Away_Team_Matches_Played_At_Home_SoFar_Goals_Against].Value = match.Away_Team_Matches_Played_At_Home_SoFar_Goals_Against;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Away_Team_Matches_Played_Away_SoFar].Value = match.Away_Team_Matches_Played_Away_SoFar;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Away_Team_Matches_Played_Away_SoFar_Goals_For].Value = match.Away_Team_Matches_Played_Away_SoFar_Goals_For;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.Away_Team_Matches_Played_Away_SoFar_Goals_Against].Value = match.Away_Team_Matches_Played_Away_SoFar_Goals_Against;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HIST_TOTAL_GAMES_BTW].Value = match.HIST_TOTAL_GAMES_BTW;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HIST_TOTAL_GAMES_BTW_HOME_WINS].Value = match.HIST_TOTAL_GAMES_BTW_HOME_WINS;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HIST_TOTAL_GAMES_BTW_HOME_WINS_PERC].Value = match.HIST_TOTAL_GAMES_BTW_HOME_WINS_PERC;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HIST_TOTAL_GAMES_BTW_DRAWS].Value = match.HIST_TOTAL_GAMES_BTW_DRAWS;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HIST_TOTAL_GAMES_BTW_DRAWS_PERC].Value = match.HIST_TOTAL_GAMES_BTW_DRAWS_PERC;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS].Value = match.HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS_PERC].Value = match.HIST_TOTAL_GAMES_BTW_AWAY_TEAM_WINS_PERC;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HIST_TOTAL_GAMES_BTW_HOME_SCORE].Value = match.HIST_TOTAL_GAMES_BTW_HOME_SCORE;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HIST_TOTAL_GAMES_BTW_AWAY_SCORE].Value = match.HIST_TOTAL_GAMES_BTW_AWAY_SCORE;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HOME_TEAM_LAST_5_GAMES_WINS].Value = match.HOME_TEAM_LAST_5_GAMES_WINS;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HOME_TEAM_LAST_5_GAMES_WINS_PERC].Value = match.HOME_TEAM_LAST_5_GAMES_WINS_PERC;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HOME_TEAM_LAST_5_GAMES_DRAWS].Value = match.HOME_TEAM_LAST_5_GAMES_DRAWS;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HOME_TEAM_LAST_5_GAMES_DRAWS_PERC].Value = match.HOME_TEAM_LAST_5_GAMES_DRAWS_PERC;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HOME_TEAM_LAST_5_GAMES_LOSSES].Value = match.HOME_TEAM_LAST_5_GAMES_LOSSES;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.HOME_TEAM_LAST_5_GAMES_LOSSES_PERC].Value = match.HOME_TEAM_LAST_5_GAMES_LOSSES_PERC;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.AWAY_TEAM_LAST_5_GAMES_WINS].Value = match.AWAY_TEAM_LAST_5_GAMES_WINS;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.AWAY_TEAM_LAST_5_GAMES_WINS_PERC].Value = match.AWAY_TEAM_LAST_5_GAMES_WINS_PERC;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.AWAY_TEAM_LAST_5_GAMES_WINS_DRAWS].Value = match.AWAY_TEAM_LAST_5_GAMES_WINS_DRAWS;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.AWAY_TEAM_LAST_5_GAMES_DRAWS_PERC].Value = match.AWAY_TEAM_LAST_5_GAMES_DRAWS_PERC;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.AWAY_TEAM_LAST_5_GAMES_LOSSES].Value = match.AWAY_TEAM_LAST_5_GAMES_LOSSES;
                worksheet.Cells[row, (int)ExportOddsFreqDataCols.AWAY_TEAM_LAST_5_GAMES_LOSSES_PERC].Value = match.AWAY_TEAM_LAST_5_GAMES_LOSSES_PERC;

                row++;
            }

            return worksheet;
        }

       
    }
}
