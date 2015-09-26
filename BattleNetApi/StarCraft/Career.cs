using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattleNetApi.StarCraft
{
    public class Career
    {
        [Obsolete("The Id field is only provided for entity framework serialization and should not be directly used")]
        public int Id { get; set; }
        public StarCraftRace Race { get; set; }
        public int ProtossWins { get; set; }
        public int TerranWins { get; set; }
        public int ZergWins { get; set; }
        public StarCraftLeague Highest1v1Rank { get; set; }
        public StarCraftLeague HighestTeamRank { get; set; }
        public int SeasonTotalGames { get; set; }
        public int CareerTotalGames { get; set; }
    }
}
