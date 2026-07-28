using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using NeuralNetGoalTools.controls;
using Microsoft.Extensions.Configuration;
using NeuralNetGoalTools.dal;
using Microsoft.EntityFrameworkCore;

namespace NeuralNetGoalTools
{
    public partial class mainForm : DevExpress.XtraBars.Ribbon.RibbonForm
    {

        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public mainForm()
        {
            InitializeComponent();

            _configuration = new ConfigurationBuilder()
           .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
           .AddJsonFile("appsettings.json")
           .Build();

            string connectionString = _configuration.GetConnectionString("ConnectionString");
            string netGoalToolsCOnStr = _configuration.GetConnectionString("CloudDb");

           

            ctrlQueries queries = new ctrlQueries(connectionString, netGoalToolsCOnStr);
            queriesNavigationPage.Controls.Add(queries);
            queries.Dock = DockStyle.Fill;
        }

        void barButtonNavigation_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            int barItemIndex = barSubItemNavigation.ItemLinks.IndexOf(e.Link);

        }
    }
}