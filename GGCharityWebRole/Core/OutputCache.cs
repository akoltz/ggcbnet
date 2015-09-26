using DevTrends.MvcDonutCaching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GGCharityWebRole.Core
{
    public class GGCharityOutputCache
    {
        public static GGCharityOutputCache Get()
        {
            return new GGCharityOutputCache();
        }

        OutputCacheManager manager;
        protected GGCharityOutputCache()
        {
            manager = new OutputCacheManager();
        }

        public void Remove(string Controller, string Action)
        {
            manager.RemoveItem("Home", "Index");
        }
    }
}