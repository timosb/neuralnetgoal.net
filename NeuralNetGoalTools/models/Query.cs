using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetGoalTools.models
{
    public class Query
    {
        public long ID { get; set; }
        public string Name { get; set; }
        public bool Country { get; set; }
        public bool League { get; set; }
        public bool HomeTeam { get; set; }
        public bool AwayTeam { get; set; }
        public bool B1NL1NF1NN1 { get; set; }
        public bool B2NL2BF2NN2 { get; set; }
        public bool Chart { get; set; }
        public bool UseTime { get; set; }
        public bool DU1 { get; set; }
        public bool DU2 { get; set; }
        public bool DUX { get; set; }
    }

}
