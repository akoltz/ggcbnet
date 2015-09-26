using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattleNetApi.StarCraft
{
    public class Match
    {
        public string Map { get; set; }
        public string Type { get; set; }
        public string Decision { get; set; }
        public string Speed { get; set; }
        public string Date { get; set; }
    }
}
