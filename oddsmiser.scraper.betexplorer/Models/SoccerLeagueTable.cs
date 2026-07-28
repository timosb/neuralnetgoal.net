using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oddsmiser.scraper.betexplorer.Models
{
    public class SoccerLeagueTable
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [StringLength(250)]
        public string Country { get; set; }
        [StringLength(250)]
        public string Division { get; set; }
        [StringLength(250)]
        public string Season { get; set; }
        public int POS { get; set; }
        public DateTime MatchDate { get; set; }
        [StringLength(250)]
        public string Team { get; set; }
        public int Matches_Played { get; set; }
        public int Matches_Won { get; set; } 
        public int Matches_Drawn { get; set; }
        public int Matches_Lost { get; set; } 
        public int Goals_For { get; set; }
        public int Goals_Against { get; set; }
        public int Goal_Difference { get; set; }
        public int Home_Matches_Played { get; set; }
        public int Home_Matches_Won { get; set; }
        public int Home_Matches_Drawn { get; set; }
        public int? Home_Matches_Lost { get; set; }
        public int? Home_Goals_For { get; set; }
        public int? Home_Goals_Against { get; set; }
        public int? Home_Goal_Difference { get; set; }
        public int? Away_Matches_Played { get; set; }
        public int? Away_Matches_Won { get; set; }
        public int? Away_Matches_Drawn { get; set; }
        public int? Away_Matches_Lost { get; set; }
        public int? Away_Goals_For { get; set; }
        public int? Away_Goals_Against { get; set; }
        public int? Away_Goal_Difference { get; set; }
        public decimal? Points { get; set; }
        public int? Ranking { get; set; }
    }
}
