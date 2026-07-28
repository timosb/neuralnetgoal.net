using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using oddsmiser.scraper.betexplorer.Models;

#nullable disable

namespace oddsmiser.scraper.betexplorer.DAL
{
    public partial class betexplorerContext : DbContext
    {
        public betexplorerContext()
        {
        }

        public betexplorerContext(DbContextOptions<betexplorerContext> options)
            : base(options)
        {
            Database.SetCommandTimeout(9000);
        }

        public virtual DbSet<SoccerResult> SoccerResults { get; set; }
        public virtual DbSet<SoccerCountryLeagues> CountryLeagues { get; set; }
        public virtual DbSet<SoccerFixture> SoccerFixtures { get; set; }
        public virtual DbSet<SoccerRunningLeague> SoccerRunningLeagues { get; set; }
        public virtual DbSet<SoccerLeagueTable> SoccerLeagueTable { get; set; }
        public virtual DbSet<SoccerDataVector> DataVectors { get; set; }
        public virtual DbSet<Errors> Errors { get; set; }
        


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=wdb4.my-hosting-panel.com;Database=neuralnetgoalData;User ID=neuralne_timos;Password=I42%9y9mm;MultipleActiveResultSets=true");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            
            modelBuilder.Entity<SoccerResult>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
