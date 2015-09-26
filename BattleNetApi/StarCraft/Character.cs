using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattleNetApi.StarCraft
{
    public class Character
    {
        public string Id { get; set; }
        public string Realm { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string ClanName { get; set; }
        public string ClanTag { get; set; }
        public string ProfilePath { get; set; }
        public Uri ProfileUrl
        {
            get
            {
                return Api.GetPublicProfileUrl(this);
            }
        }

        public Icon Portrait { get; set; }
        public Icon Avatar { get; set; }
        public Career Career { get; set; }
        public Region Region
        {
            get;
            set;
        }
    }
}
