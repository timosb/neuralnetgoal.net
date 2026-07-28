using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oddsmiser.scraper.betexplorer.Models
{
    public class SoccerFixture
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ID { get; set; }
        [Required]
        [StringLength(250)]
        public string Country { get; set; }
        [Required]
        [StringLength(250)]
        public string Division { get; set; }
        [Required]
        [StringLength(250)]
        public string Seazon { get; set; }
        [Column(TypeName = "datetime")]
        public DateTime MatchDate { get; set; }
        [Required]
        [StringLength(500)]
        public string HomeTeam { get; set; }
        [Required]
        [StringLength(500)]
        public string AwayTeam { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? AvgHomeOdds { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? AvgDrawOdds { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? AvgAwayOdds { get; set; }
    }
}
