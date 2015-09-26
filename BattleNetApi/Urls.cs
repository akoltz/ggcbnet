using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattleNetApi
{
    public static class Urls
    {
        public static IReadOnlyDictionary<Region, Uri> UrlsByRegion;

        static Urls()
        {
            var urlsByRegion = new Dictionary<Region, Uri>();

            urlsByRegion.Add(Region.US, new Uri(@"https://us.api.battle.net"));
            urlsByRegion.Add(Region.Taiwan, new Uri(@"https://tw.api.battle.net"));
            urlsByRegion.Add(Region.Sea, new Uri(@"https://sea.api.battle.net"));
            urlsByRegion.Add(Region.Korea, new Uri(@"https://kr.api.battle.net"));
            urlsByRegion.Add(Region.China, new Uri(@"https://api.battlenet.com.cn"));
            urlsByRegion.Add(Region.Europe, new Uri(@"https://eu.api.battle.net"));

            UrlsByRegion = urlsByRegion;
        }
    }
}
