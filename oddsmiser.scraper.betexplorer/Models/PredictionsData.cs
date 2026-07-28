using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oddsmiser.scraper.betexplorer.Models
{
    public class PredictionsData
    {
        public int ID { get; set; }

        public string COUNTRY { get; set; }

        public string LEAGUE { get; set; }

        public string HOME { get; set; }

        public string AWAY { get; set; }

        public int? B1 { get; set; }

        public int? NL1 { get; set; }

        public int? NF1 { get; set; }

        public int? NN1 { get; set; }

        public int? B2 { get; set; }

        public int? NL2 { get; set; }

        public int? NF2 { get; set; }

        public int? NN2 { get; set; }

        public string TREND { get; set; }
    }
}
