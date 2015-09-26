using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BattleNetApi.StarCraft
{
    public static class Api
    {
        public static IReadOnlyDictionary<Region, Uri> PublicProfileUrlsByRegion;

        static Api()
        {
            var urls = new Dictionary<Region, Uri>();

            urls.Add(Region.US, new Uri(@"http://us.battle.net/"));
            urls.Add(Region.Taiwan, new Uri(@"http://tw.battle.net/"));
            urls.Add(Region.Sea, new Uri(@"http://sea.battle.net/"));
            urls.Add(Region.Korea, new Uri(@"http://kr.battle.net/"));
            urls.Add(Region.China, new Uri(@"http://www.battlenet.com.cn/"));
            urls.Add(Region.Europe, new Uri(@"http://eu.battle.net/"));

            PublicProfileUrlsByRegion = urls;
        }

        public async static Task<Profile> GetProfileAsync(Region region, string accessToken)
        {
            if (String.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException("accessToken");
            }

            Uri profileUrl = GetProfileApiUrl(region, accessToken);

            string profileApiResult = await ApiDispatcher.MakeRequestAsync(profileUrl);
            return Profile.FromJsonString(profileApiResult, region);
        }

        internal static Uri GetPublicProfileUrl(Character character)
        {
            Uri profileUrl;
            if (!Uri.TryCreate(PublicProfileUrlsByRegion[character.Region], @"sc2" + character.ProfilePath, out profileUrl))
            {
                throw new InvalidOperationException("Failed to create a URI for the requested profile.");
            }
            return profileUrl;
        }

        internal static Uri GetProfileApiUrl(Region region, string accessToken)
        {
            Uri profileUrl;
            if (!Uri.TryCreate(Urls.UrlsByRegion[region], @"sc2/profile/user?access_token=" + accessToken, out profileUrl))
            {
                throw new InvalidOperationException("Failed to create a URI for the requested profile.");
            }
            return profileUrl;
        }

        public static async Task<IEnumerable<Match>> GetMatchHistoryAsync(Region region, string id, string name, string apiKey)
        {
            if (String.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id");
            }

            if (String.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("name");
            }

            if (String.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("API Key has not been configured");
            }

            string path = String.Format(CultureInfo.InvariantCulture, @"sc2/profile/{0}/{1}/{2}/matches?locale=en_US&apiKey={3}", id, (int)region, name, apiKey);
            Uri matchHistoryUri;
            if (!Uri.TryCreate(Urls.UrlsByRegion[region], path, out matchHistoryUri))
            {
                throw new InvalidOperationException("Failed to create a URI for the requested player's match history.");
            }

            string matchHistoryResult = await ApiDispatcher.MakeRequestAsync(matchHistoryUri);
            MatchHistory matches = JsonConvert.DeserializeObject<MatchHistory>(matchHistoryResult);
            return matches.Matches;
        }
    }
}

