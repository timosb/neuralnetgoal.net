using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetGoalTools.models
{
    public class SummaryStatsModel
    {
        public string Country { get; set; }
        public string League { get; set; }
        public DateTime MatchDate { get; set; }
        public string Home { get; set; }
        public string Away { get; set; }
        public decimal Book1 { get; set; }
        public decimal Book2 { get; set; }
        public decimal BookX { get; set; }
        public decimal NLT1 { get; set; }
        public decimal NLT2 { get; set; }
        public decimal NLTX { get; set; }
        public int HomeRank { get; set; }
        public int AwayRank { get; set; }
        public int HomeLast4Goals { get; set; }
        public int AwayLast4Goals { get; set; }
        public int? B1 { get; set; }
        public int? NL1 { get; set; }
        public int? NF1 { get; set; }
        public int? NN1 { get; set; }
        public int? B2 { get; set; }
        public int? NL2 { get; set; }
        public int? NF2 { get; set; }
        public int? NN2 { get; set; }
        public string TREND { get; set; }
        public string CI { get; set; }
        public string CJ { get; set; }
        public string CK { get; set; }
    }

}
