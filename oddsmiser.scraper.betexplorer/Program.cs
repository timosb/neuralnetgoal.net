using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using oddsmiser.scraper.betexplorer.DAL;
using Serilog;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace oddsmiser.scraper.betexplorer
{
    public class Program
    {
        static readonly Stopwatch timer = new Stopwatch();
        public static async Task Main(string[] args)
        {
           var host  = CreateHostBuilder(args);

            var histSoccerData = ActivatorUtilities.CreateInstance<SoccerHistorical>(host.Services);

            timer.Start();
            Console.WriteLine("Proccess Started... Getting Fixures!");

            await histSoccerData.GetLatestResultsAsynch();
            await histSoccerData.BuildLeagueTables();
            
            await histSoccerData.Fixtures();

            timer.Stop();
            Console.WriteLine($"Proccess Finished! Execution Time : {timer.Elapsed.ToString()}");
            Console.ReadLine();
        }

        static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Set up configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            services.AddSingleton<IConfiguration>(configuration);

            Log.Logger = new LoggerConfiguration().MinimumLevel.Error()
                       .WriteTo.File("logs\\oddsmiser.scrapper.betexplorer.log", rollingInterval: RollingInterval.Day)
                       .WriteTo.Console()
                       .CreateLogger();

            services.AddOptions();
            // Add logging
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.AddSerilog();
                loggingBuilder.AddDebug();
            });

            services.AddDbContext<betexplorerContext>(options =>
            options.UseSqlServer(
              configuration.GetConnectionString("DefaultConnection"))).AddSingleton<IConfiguration>(configuration);

            var emailConfig = configuration.GetSection("SmtpSettings").Get<SmtpSettings>();

            services.AddSingleton<SmtpSettings>(emailConfig);
            services.AddSingleton<IEmailSender, EmailSender>();

            services.AddSingleton<ISoccerHistorical, SoccerHistorical>();

            return services.BuildServiceProvider();
        }


        public static IHost CreateHostBuilder(string[] args) {

            return Host.CreateDefaultBuilder(args)
                  .ConfigureServices((hostContext, services) =>
                   {
                       IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json", true, true).Build();

                       var emailConfig = config.GetSection("SmtpSettings").Get<SmtpSettings>();


                       Log.Logger = new LoggerConfiguration().MinimumLevel.Error()
                       .WriteTo.File("logs\\oddsmiser.scrapper.betexplorer.log", rollingInterval: RollingInterval.Day)
                       .WriteTo.Console()
                       .CreateLogger();

                       services.AddOptions();
                       // Add logging
                       services.AddLogging(loggingBuilder =>
                          {
                              loggingBuilder.AddConsole();
                              loggingBuilder.AddSerilog();
                              loggingBuilder.AddDebug();
                          });

                       services.AddDbContext<betexplorerContext>(options =>
                         options.UseSqlServer(
                         config.GetConnectionString("DefaultConnection"))).AddSingleton<IConfiguration>(config);


                       //services.AddDbContext<cloudDbContext>(options =>
                       //         options.UseMySql(config.GetConnectionString("NeuralConnection"), ServerVersion.AutoDetect(config.GetConnectionString("NeuralConnection")))).AddSingleton<IConfiguration>(config);

                       services.AddSingleton<SmtpSettings>(emailConfig);
                       services.AddSingleton<IEmailSender, EmailSender>();

                       services.AddSingleton<IParser, Parser>();
                       services.AddSingleton<ISoccerHistorical, SoccerHistorical>();
                       

                   }).Build();

        }
         
    }
}
