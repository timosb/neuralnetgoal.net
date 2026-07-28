using oddsmiser.scraper.betexplorer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oddsmiser.scraper.betexplorer
{
    public interface IParser
    {
        Task<(List<Errors> errors, List<SoccerResult> soccerResults)> GetCountryLeagueResultsAsynch(string country, string season, string league, string url);
        Task<List<SoccerCountryLeagues>> GetCountryLeagues(string url, string country);
        Task<List<SoccerFixture>> GetCountryLeagueFixtures(string country, string season, string league, string url);
    }
}
