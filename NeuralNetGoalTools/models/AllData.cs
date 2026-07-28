using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetGoalTools.models
{
    public class AllData
    {
        public long ID { get; set; }
        public string COUNTRY { get; set; }
        public string LEAGUE { get; set; }
        public DateTime? MATCHDATE { get; set; }
        public string HOME { get; set; }
        public string AWAY { get; set; }
        public string FT { get; set; }
        public string HT { get; set; }
        public decimal? BOOK_1 { get; set; }
        public decimal? BOOK_X { get; set; }
        public decimal? BOOK_2 { get; set; }
        public decimal? NLT_1 { get; set; }
        public decimal? NLT_X { get; set; }
        public decimal? NLT_2 { get; set; }
        public decimal? NAT_1 { get; set; }
        public decimal? NAT_X { get; set; }
        public decimal? NAT_2 { get; set; }
        public decimal? NFT_1 { get; set; }
        public decimal? NFT_X { get; set; }
        public decimal? NFT_2 { get; set; }
        public int? B1 { get; set; }
        public int? NL1 { get; set; }
        public int? NF1 { get; set; }
        public int? NN1 { get; set; }
        public int? B2 { get; set; }
        public int? NL2 { get; set; }
        public int? NF2 { get; set; }
        public int? NN2 { get; set; }
        public string CHART { get; set; }
        public string CI { get; set; }
        public string CJ { get; set; }
        public string CK { get; set; }

    }
}
