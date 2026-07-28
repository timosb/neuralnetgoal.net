using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetGoalTools.models
{
    public class SoccerTeamBudget
    {
        public long ID { get; set; }

        [Required]
        [StringLength(500)]
        public string TeamName { get; set; }

        [StringLength(50)]
        public string? PlayersValue { get; set; }

        [StringLength(50)]
        public string? Salaries { get; set; }
    }
}
