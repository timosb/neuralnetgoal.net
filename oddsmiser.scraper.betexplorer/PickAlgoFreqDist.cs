using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oddsmiser.scraper.betexplorer
{
    public class PickAlgoFreqDist : IPickAlgoFreqDist
    {
        private readonly IConfiguration _config;
        public PickAlgoFreqDist(IConfiguration config)
        {
                
        }
        public void UploadFile(string path)
        {
            throw new NotImplementedException();
        }
    }
}
