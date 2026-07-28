using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeuralNetGoalTools.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetGoalTools.dal
{
    public class AppDbContext : DbContext
    {
        public DbSet<AllData> AllData { get; set; }
        public DbSet<PredictionsData> predictionsData { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
    }
}
