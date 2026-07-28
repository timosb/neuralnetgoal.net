using System.Threading.Tasks;

namespace oddsmiser.scraper.betexplorer
{
    public interface ISoccerHistorical
    {
        public Task InitCountryLeagueLinksTable();
        public Task<bool> GetHistoricalDataAsynch();
        public Task<bool> GetHistoricalDataAsynch(string Country);
        public Task<bool> GetHistoricalDataAsynch(string Country,string Division);
        public Task<bool> GetHistoricalDataAsynch(string Country, string Division, string Season);
        public Task GetLatestResultsAsynch();
        public Task Fixtures();
        public Task<bool> BuildLeagueTables();
        public Task<bool> BuildLeagueTablesHistory();
        public Task<bool> ExportAsynch();

    }
}
