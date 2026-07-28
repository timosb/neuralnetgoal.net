using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NeuralNetGoalTools.dal;

namespace NeuralNetGoalTools
{
   
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlite("Data Source=neuralNetGoalTools.db");

            return new AppDbContext(optionsBuilder.Options);
        }
    }

}
