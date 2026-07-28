using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oddsmiser.scraper.betexplorer
{
    //"Country": "england",
    //"League": "premier-league",
    //"YearFrom": "2020",
    //"YearTo": "2021"
    [JsonObject("ScrapQuery")]
    public class ScrapQuery
    {
        [JsonProperty("Country")]
        public string Country { get; set; }
        [JsonProperty("League")]
        public string League { get; set; }
        [JsonProperty("YearFrom")]
        public string YearFrom { get; set; }
        [JsonProperty("YearTo")]
        public string YearTo { get; set; }
    }
}
