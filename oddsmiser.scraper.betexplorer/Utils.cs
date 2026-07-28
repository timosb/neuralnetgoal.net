using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace oddsmiser.scraper.betexplorer
{
    public static class Utils
    {
        public static DateTime GetMatchDate(string sdate)
        {
            //14,4,2021,19,00

            var datePart = sdate.Split('-')[0].Trim();
            var timePart = sdate.Split('-')[1].Trim();

            
            var year = datePart.Split('.')[2].Trim();
            var month = datePart.Split('.')[1].Trim();
            var day = datePart.Split('.')[0].Trim();
            var hh  = timePart.Split(':')[0].Trim();
            var min = timePart.Split(':')[1].Trim();

            if (month.Length == 1)
                month = "0" + month;

            var dateStringToConvert = $"{year}-{month}-{day} {hh}:{min}";
            var s = DateTime.Parse(dateStringToConvert);

            return DateTime.ParseExact(dateStringToConvert,"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture);

            
        }

        public static DateTime SoccerMatchDate(string sdate)
        {
            var datePart = sdate.Split("-")[0].Trim();
            var timePart = sdate.Split("-")[1].Trim();

            var day = datePart.Split(".")[0].Trim();
            var month = datePart.Split(".")[1].Trim();
            var year = datePart.Split(".")[2].Trim();

            return new DateTime(int.Parse(year), int.Parse(month), int.Parse(day), int.Parse(timePart.Split(":")[0]), int.Parse(timePart.Split(":")[1]), 0);

        }

        //22,5,2022,17,00
        public static DateTime SoccerMatchDate2(string sdate)
        {
            

            var day = sdate.Split(",")[0].Trim();
            var month = sdate.Split(",")[1].Trim();
            var year = sdate.Split(",")[2].Trim();

            var hh = sdate.Split(",")[3].Trim();
            var min = sdate.Split(",")[4].Trim();
            
            return new DateTime(int.Parse(year), int.Parse(month), int.Parse(day), int.Parse(hh), int.Parse(min), 0);
        }

        public static (int homeFirst,int homeSecond,int homeThird,int homeFourth,
            int awayFirst,int awaySecond,int awayThird,int awayFourth) BasketBallGetScoresPerPeriod(string scores)
        {
            int homeFirst, homeSecond,homeThird,homeFourth;
            int awayFirst,awaySecond,awayThird,awayFourth;

            scores = scores.Replace("(","").Replace(")","");
            homeFirst = int.Parse(scores.Split(",")[0].Split(":")[0]);
            homeSecond = int.Parse(scores.Split(",")[1].Split(":")[0]);
            homeThird = int.Parse(scores.Split(",")[2].Split(":")[0]);
            homeFourth = int.Parse(scores.Split(",")[3].Split(":")[0]);

            awayFirst = int.Parse(scores.Split(",")[0].Split(":")[1]);
            awaySecond = int.Parse(scores.Split(",")[1].Split(":")[1]);
            awayThird = int.Parse(scores.Split(",")[2].Split(":")[1]);
            awayFourth = int.Parse(scores.Split(",")[3].Split(":")[1]);

            return (homeFirst,homeSecond,homeThird,homeFourth,awayFirst,awaySecond,awayThird,awayFourth);

        }

        public static (int homeFirstHalf, int homeSecondHalf, int awayFirstHalf, int awaySecondHalf) SoccerGetScoresPerPeriod(string scores)
        {
            int homeFirst, homeSecond;
            int awayFirst, awaySecond;

            scores = scores.Replace("(", "").Replace(")", "");
            
            homeFirst = int.Parse(scores.Split(",")[0].Split(":")[0]);
            homeSecond = int.Parse(scores.Split(",")[1].Split(":")[0]);
            

            awayFirst = int.Parse(scores.Split(",")[0].Split(":")[1]);
            awaySecond = int.Parse(scores.Split(",")[1].Split(":")[1]);
            
            return (homeFirst, homeSecond, awayFirst, awaySecond);

        }        
    }
}
