using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetGoalTools.models
{
    public class QueryResults
    {
        public string COUNTRY { get; set; }
        public string LEAGUE { get; set; }
        public DateTime? MATCHDATE { get; set; }
        public string HOME { get; set; }
        public string AWAY { get; set; }
        public Int64? HWINS { get; set; }
        public Int64? DRAWS { get; set; }
        public Int64? AWINS { get; set; }
        public Int64? UNDER { get; set; }
        public Int64? OVER { get; set; }
        public Int64? GG { get; set; }
        public Int64? NG { get; set; }
        public List<QueryMatches> Matches { get; set; }
    }

}
