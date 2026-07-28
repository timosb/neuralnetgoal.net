using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using oddsmiser.scraper.betexplorer.DAL;
using oddsmiser.scraper.betexplorer.Models;
using Org.BouncyCastle.Crypto.Prng.Drbg;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace oddsmiser.scraper.betexplorer
{
    public class Parser : IParser
    {
        private readonly ILogger<SoccerHistorical> _loger;
        private readonly IConfiguration _config;
        public readonly string baseULR = "https://www.betexplorer.com";
        private readonly Stopwatch timer = new Stopwatch();

        public Parser(ILogger<SoccerHistorical> logger, IConfiguration config)
        {
            this._loger = logger;
            this._config = config;
        }
        public async Task<(List<Errors> errors, List<SoccerResult> soccerResults)> GetCountryLeagueResultsAsynch(string country, string season, string league, string url)
        {
            var results = new List<SoccerResult>();
            var errors = new List<Errors>();    

            if (timer.IsRunning) { timer.Restart(); }
            timer.Start();
            try
            {
                _loger.LogInformation($"Getting Results County: {country}, Season: {season} League: {league}");

                HtmlWeb web = new HtmlWeb();
                HtmlDocument document = await web.LoadFromWebAsync(this.baseULR + url + @"results/");

                var xpath_tab = @"//*[@id='sm-0-0']/div/ul";
                var ulTab = document.DocumentNode.SelectNodes(xpath_tab);


                var xpath_sel_a = @"//*[@id='sm-0-0']/div/ul/li[2]/a";
                var a1 = document.DocumentNode.SelectNodes(xpath_sel_a);


                var round = (a1 == null ? "Regular Season" : a1.First().InnerText);

                var tab = await ParseResultTab(document, country, season, league, round);
                if (tab.soccerResults != null && tab.soccerResults.Count > 0) results.AddRange(tab.soccerResults);
                if (tab.errors != null && tab.errors.Count > 0) errors.AddRange(tab.errors);

                if (ulTab != null)
                {

                    var liList = ulTab.First().ChildNodes.Where(x => x.Name == "li").ToList();
                    foreach (var li in liList)
                    {
                        var aList = li.ChildNodes.Where(x => x.Name == "a").ToList();
                        foreach (var a in aList)
                        {
                            var href = a.Attributes["href"].Value;
                            var txt = a.InnerText;
                            if (!a.HasClass("current"))
                            {
                                document = await web.LoadFromWebAsync(this.baseULR + url + @"results/" + href);
                                var tabResults = await ParseResultTab(document, country, season, league, txt);
                                if (tabResults.soccerResults != null && tabResults.soccerResults.Count > 0) results.AddRange(tabResults.soccerResults);
                                if (tabResults.errors != null && tabResults.errors.Count > 0) errors.AddRange(tabResults.errors);
                            }
                        }
                    }

                }
                timer.Stop();
                _loger.LogInformation($"Finish Results for County: {country}, Season: {season} League: {league} -> Execution Time : {timer.Elapsed.ToString()}");

            }
            catch (Exception ex)
            {
                _loger.LogError(ex.Message);
                
            }

            return (errors,results);
        }

        private async Task<(List<Errors> errors, List<SoccerResult> soccerResults)> ParseResultTab(HtmlDocument document, string country, string season, string league, string round)
        {
            var results = new ConcurrentBag<SoccerResult>();
            var errors = new ConcurrentBag<Errors>();
            try
            {
                var xpath_country_leagues = @"//*[@id='js-leagueresults-all']/div/div/table";
                var tBody = document.DocumentNode.SelectNodes(xpath_country_leagues);
                if (tBody == null) {
                    errors.Add(new Errors()
                    {
                        Message = $"Problem Loading Data Country: {country}, Seazon: {season}, League: {league}",
                        RunTime = DateTime.Now,
                        ULR = document.ToString() 
                    });

                    return (errors.ToList(), null);
                }

                var tasks = new List<Task>();
                SemaphoreSlim semaphore = new SemaphoreSlim(20);

                foreach (var link in tBody)
                {
                    var trList = link.ChildNodes.Where(x => x.Name == "tr").ToList();

                    foreach (var tr in trList)
                    {
                        if (tr.ChildNodes.First().Name == "th") continue;

                        var tdList = tr.ChildNodes.ToList();

                        tasks.Add(Task.Run(async () => {
                            await semaphore.WaitAsync();
                            try
                            {
                                var sri = await ParseMatchLink(tdList, country, season, league, round);

                                if (sri.Item1.Count > 0)
                                {
                                    foreach (var err in sri.Item1)
                                    {
                                        _loger.LogError(err.Message);
                                        errors.Add(err);
                                    }

                                }
                                else
                                {
                                    results.Add(sri.Item2);
                                }
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }));
                    }
                }

                await Task.WhenAll(tasks);
                


            }
            catch (Exception ex)
            {
                _loger.LogError(ex.Message);
            }
            return (errors.ToList(),results.ToList());
        }



        private async Task<(List<Errors>, SoccerResult)> ParseMatchLink(List<HtmlNode> tdList, string country, string season, string league, string round)
        {
            var soccerMatch = new SoccerResult();
            var errors = new List<Errors>();


            soccerMatch.Country = country;
            soccerMatch.Division = league;
            soccerMatch.Season = season;


            var matchDetailsLink = tdList[1].ChildNodes[0].Attributes["href"].Value;
            var matchDetailsUrl = baseULR + matchDetailsLink;

            HtmlWeb web = new HtmlWeb();

            var docDetails = await web.LoadFromWebAsync(matchDetailsUrl);

            var xpath_match_date = @"//*[@id='match-date']";

            var i = 0;
            while (docDetails.DocumentNode.SelectNodes(@"//div[@class='list-details__item__title teamsLink']") == null)
            {
                docDetails = await web.LoadFromWebAsync(matchDetailsUrl);
                i++;
                if (i == 10) break;
            }

            if (docDetails.DocumentNode.SelectNodes(@"//div[@class='list-details__item__title teamsLink']") == null)
            {
                var teams = tdList[3].InnerText;
                errors.Add(new Errors()
                {
                    Message = $"Problem while loading the page for the Game -> Country: {country}, Seazon: {season}, League: {league}, Teams: {teams} "
                    ,
                    RunTime = DateTime.Now,
                    ULR = matchDetailsUrl
                });
            }

            var homeTeam = docDetails.DocumentNode.SelectNodes(@"//div[@class='list-details__item__title teamsLink']")[0].ChildNodes[0].InnerText;
            var awayTeam = docDetails.DocumentNode.SelectNodes(@"//div[@class='list-details__item__title teamsLink']")[1].ChildNodes[0].InnerText;

            try
            {
                soccerMatch.HomeTeam = homeTeam;
                soccerMatch.AwayTeam = awayTeam;

                var elmatchDate = docDetails.DocumentNode.SelectNodes(xpath_match_date).FirstOrDefault();

                if (elmatchDate != null) soccerMatch.MatchDate = Utils.SoccerMatchDate2(elmatchDate.Attributes["data-dt"].Value);


                //js-eventstage

                var xpath_match_evstage = @"//*[@id='js-eventstage']";

                var elmatchEvStage = docDetails.DocumentNode.SelectNodes(xpath_match_evstage);

                var msgCanceled = "";

                if (elmatchEvStage != null)
                {
                    msgCanceled = elmatchEvStage.FirstOrDefault().InnerText;
                    errors.Add(new Errors()
                    {
                        Message = $"Problem parsing Country: {country}, Seazon: {season}, League: {league}, Teams: {homeTeam} - {awayTeam} {msgCanceled}",
                        RunTime = DateTime.Now,
                        ULR = matchDetailsUrl
                    });

                    return (errors, null);
                }

                var xpath_match_scores = @"//*[@id='js-score']";

                var elmatchScore = docDetails.DocumentNode.SelectNodes(xpath_match_scores);



                if (elmatchScore != null && elmatchScore.FirstOrDefault().InnerText.Split(":").Count() > 0)
                {
                    var isScoreCorrect = true;
                    if (elmatchScore.FirstOrDefault().InnerText.Split(":")[0].Trim().Length == 0) isScoreCorrect = false;
                    if (elmatchScore.FirstOrDefault().InnerText.Split(":")[1].Trim().Length == 0) isScoreCorrect = false;

                    if (!isScoreCorrect)
                    {
                        errors.Add(new Errors()
                        {
                            Message = $"Problem parsing Country: {country}, Seazon: {season}, League: {league}, Teams: {homeTeam} - {awayTeam}",
                            RunTime = DateTime.Now,
                            ULR = matchDetailsUrl
                        });
                        return (errors, null);
                    }

                    soccerMatch.HomeScore = int.Parse(elmatchScore.FirstOrDefault().InnerText.Split(":")[0]);
                    soccerMatch.AwayScore = int.Parse(elmatchScore.FirstOrDefault().InnerText.Split(":")[1]);
                }

                var xpath_match_scores_hf = @"//*[@id='js-partial']";

                var elmatchScores = docDetails.DocumentNode.SelectNodes(xpath_match_scores_hf);

                if (elmatchScores != null)
                {
                    var scores = Utils.SoccerGetScoresPerPeriod(elmatchScores.First().InnerText);
                    soccerMatch.HalfTimeHomeScore = scores.homeFirstHalf;
                    soccerMatch.HalfTimeAwayScore = scores.awayFirstHalf;

                }

                if (tdList[2].InnerText != "&nbsp;")
                    soccerMatch.AvgHomeOdds = tdList[2].HasClass("colored") ? decimal.Parse(tdList[2].LastChild.LastChild.LastChild.Attributes["data-odd"].Value) : decimal.Parse(tdList[2].Attributes["data-odd"].Value);
                if (tdList[3].InnerText != "&nbsp;")
                    soccerMatch.AvgDrawOdds = tdList[3].HasClass("colored") ? decimal.Parse(tdList[3].LastChild.LastChild.LastChild.Attributes["data-odd"].Value) : decimal.Parse(tdList[3].Attributes["data-odd"].Value);
                if (tdList[4].InnerText != "&nbsp;")
                    soccerMatch.AvgAwayOdds = tdList[4].HasClass("colored") ? decimal.Parse(tdList[4].LastChild.LastChild.LastChild.Attributes["data-odd"].Value) : decimal.Parse(tdList[4].Attributes["data-odd"].Value);

                soccerMatch.Round = round;


            }
            catch (Exception)
            {
                errors.Add(new Errors()
                {
                    Message = $"Problem parsing Country: {country}, Seazon: {season}, League: {league}, Teams: {homeTeam} - {awayTeam}",
                    RunTime = DateTime.Now,
                    ULR = matchDetailsUrl
                });

                soccerMatch = null;
            }

            return (errors, soccerMatch);

        }

        public async Task<List<SoccerCountryLeagues>> GetCountryLeagues(string url, string country)
        {
            var result = new List<SoccerCountryLeagues>();
            try
            {

                _loger.LogInformation("Getting League Links");

                HtmlWeb web = new HtmlWeb();
                HtmlDocument document = await web.LoadFromWebAsync(url);
                var xpath_country_leagues = @"//table/tbody";
                var tBody = document.DocumentNode.SelectNodes(xpath_country_leagues);
                if (tBody == null) return result;
                foreach (var link in tBody)
                {
                    var trList = link.ChildNodes.Where(x => x.Name == "tr").ToList();
                    var season = trList.First().ChildNodes.FirstOrDefault();

                    foreach (var tr in trList)
                    {
                        if (tr.ChildNodes.First().Name == "th") continue;
                        var tdList = tr.ChildNodes.ToList();
                        var countryLeague = new SoccerCountryLeagues();
                        countryLeague.Country = country;
                        countryLeague.Division = tdList[0].InnerText;
                        countryLeague.URL = tdList[0].ChildNodes[0].Attributes["href"].Value;
                        countryLeague.Season = season.InnerText;
                        result.Add(countryLeague);
                    }
                }

            }
            catch (Exception ex)
            {

                _loger.LogError(ex.Message);
            }

            return result;
        }

        public async Task<List<SoccerFixture>> GetCountryLeagueFixtures(string country, string season, string league, string url)
        {
            var results = new ConcurrentBag<SoccerFixture>();

            try
            {
                _loger.LogInformation($"Getting Fixtures County: {country}, Season: {season} League: {league}");

                HtmlWeb web = new HtmlWeb();
                HtmlDocument document = await web.LoadFromWebAsync(this.baseULR + url + @"fixtures/");
                var xpath_country_leagues = @"//*[@id='js-leaguefixtures-all']/div/div/table";
                var tBody = document.DocumentNode.SelectNodes(xpath_country_leagues);
                if (tBody == null) return results.ToList();

                var tasks = new List<Task>();
                SemaphoreSlim semaphore = new SemaphoreSlim(1);

                foreach (var link in tBody)
                {
                    var trList = link.ChildNodes.Where(x => x.Name == "tr").ToList();

                    foreach (var tr in trList)
                    {
                        if (tr.ChildNodes.First().Name == "th") continue;

                        var tdList = tr.ChildNodes.ToList();
                        var matchDetailsLink = tdList[3].ChildNodes[0].Attributes["href"].Value;
                        var matchDetailsUrl = baseULR + matchDetailsLink;

                        var soccerMatch = new SoccerFixture();
                        soccerMatch.Country = country;
                        soccerMatch.Division = league;
                        soccerMatch.Seazon = season;
                        try
                        {
                            var odds = tr.ChildNodes.Where(x => x.HasClass("table-main__odds"));
                            if (odds == null) continue;
                            var oddsList = odds.ToList();
                            if (oddsList.Count == 0) continue;
                            if (oddsList[0].InnerText != "&nbsp;")
                                soccerMatch.AvgHomeOdds = decimal.Parse(oddsList[0].ChildNodes.First().Attributes["data-odd"].Value);
                            if (soccerMatch.AvgHomeOdds.HasValue == false) continue;
                            if (oddsList[1].InnerText != "&nbsp;")
                                soccerMatch.AvgDrawOdds = decimal.Parse(oddsList[1].ChildNodes.First().Attributes["data-odd"].Value);
                            if (soccerMatch.AvgDrawOdds.HasValue == false) continue;
                            if (oddsList[2].InnerText != "&nbsp;")
                                soccerMatch.AvgAwayOdds = decimal.Parse(oddsList[2].ChildNodes.First().Attributes["data-odd"].Value);
                            if (soccerMatch.AvgAwayOdds.HasValue == false) continue;
                        }
                        catch (Exception)
                        {
                            _loger.LogError($"Problem parsing {matchDetailsLink}");
                            continue;
                        }
                       

                        tasks.Add(Task.Run(async () => {
                            await semaphore.WaitAsync();
                            try
                            {
                                var sri = await SoccerFixtureAsync(soccerMatch, tdList);

                                if (sri != null)
                                {                                   
                                    results.Add(sri);
                                }
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }));
                    }
                }

                await Task.WhenAll(tasks);

                              

            }
            catch (Exception ex)
            {

                _loger.LogError(ex.Message);
            }

            return results.ToList();
        }

        private async Task<SoccerFixture> SoccerFixtureAsync(SoccerFixture soccerMatch,List<HtmlNode> tdList)
        {
            
            try
            {
                var matchDetailsLink = tdList[3].ChildNodes[0].Attributes["href"].Value;
                var matchDetailsUrl = baseULR + matchDetailsLink;

                HtmlWeb web = new HtmlWeb();
                var docDetails = await web.LoadFromWebAsync(matchDetailsUrl);

                var xpath_match_date = @"//*[@id='match-date']";


                var i = 0;
                while (docDetails.DocumentNode.SelectNodes(@"//div[@class='list-details__item__title teamsLink']") == null)
                {
                    docDetails = await web.LoadFromWebAsync(matchDetailsUrl);
                    i++;
                    if (i == 10) break;
                }

                if (docDetails.DocumentNode.SelectNodes(@"//div[@class='list-details__item__title teamsLink']") == null)
                {
                    var teams = tdList[3].InnerText.Split("-");
                    _loger.LogError($"Problem parsing Results for Game -> Country: {soccerMatch.Country}, Seazon: {soccerMatch.Seazon}, League: {soccerMatch.Division}, Teams: {teams} ");
                    return null;
                }

                var homeTeam = docDetails.DocumentNode.SelectNodes(@"//div[@class='list-details__item__title teamsLink']")[0].ChildNodes[0].InnerText;
                var awayTeam = docDetails.DocumentNode.SelectNodes(@"//div[@class='list-details__item__title teamsLink']")[1].ChildNodes[0].InnerText;

                //var teams = tdList[3].InnerText.Split("-");
                //var homeTeam = teams[0].Trim();
                //var awayTeam = teams[1].Trim();

                soccerMatch.HomeTeam = homeTeam;
                soccerMatch.AwayTeam = awayTeam;

                var elmatchDate = docDetails.DocumentNode.SelectNodes(xpath_match_date).FirstOrDefault();

                if (elmatchDate != null) soccerMatch.MatchDate = Utils.SoccerMatchDate2(elmatchDate.Attributes["data-dt"].Value);

                return soccerMatch;
            }
            catch (Exception ex)
            {
                _loger.LogError(ex.Message);
                return null;
            }
        }
       
    }
}
