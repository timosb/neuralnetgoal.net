using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oddsmiser.scraper.betexplorer.Models
{
    public class SoccerRunningLeague
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
        [StringLength(500)]
        public string URL { get; set; }

    }
}
