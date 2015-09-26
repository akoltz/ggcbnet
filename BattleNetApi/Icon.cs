using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattleNetApi
{
    public class Icon
    {
        [Obsolete("The Id field is only provided for entity framework serialization and should not be directly used")]
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public int Offset { get; set; }
        public string Url { get; set; }
    }
}
