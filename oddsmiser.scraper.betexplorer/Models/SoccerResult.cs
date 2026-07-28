using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

#nullable disable

namespace oddsmiser.scraper.betexplorer.Models
{
    public partial class SoccerResult
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
        public DateTime MatchDate { get; set; }
        [StringLength(250)]
        public string HomeTeam { get; set; }
        [StringLength(250)]
        public string AwayTeam { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public int HalfTimeHomeScore { get; set; }
        public int HalfTimeAwayScore { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal AvgHomeOdds { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal AvgDrawOdds { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal AvgAwayOdds { get; set; }
        public string Round { get; set; }
     
    }
}
