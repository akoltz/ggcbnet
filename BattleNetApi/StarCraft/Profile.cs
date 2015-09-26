using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattleNetApi.StarCraft
{
    public class Profile
    {
        public List<Character> Characters { get; set; }
        public string JsonString { get; set; }
        public int Version { get; private set; }
        public Region Region { get; private set; }

        internal static Profile FromJsonString(string json, Region region)
        {
            Profile profile = JsonConvert.DeserializeObject<Profile>(json);
            profile.JsonString = json;
            profile.Region = region;
            profile.Version = 1;
            return profile;
        }

        public static Profile FromJsonString(string json, int version, Region region)
        {
            if (version != 1)
            {
                throw new NotSupportedException();
            }

            Profile profile = JsonConvert.DeserializeObject<Profile>(json);
            profile.JsonString = json;
            profile.Version = 1;

            foreach (var character in profile.Characters)
            {
                character.Region = region; 
            }
            return profile;
        }
    }
}
