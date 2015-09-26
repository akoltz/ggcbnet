using AzureCore;
using CloudCore.Caching;
using CloudCore.Storage;
using CloudCore.Utilities;
using GGCharityData;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Practices.EnterpriseLibrary.WindowsAzure.TransientFaultHandling.SqlAzure;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Web;
using System.Linq;
using System.Threading.Tasks;
using CloudCore;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using GGCharityCore;

namespace GGCharityWebRole
{
    public static class GGCharityCache
    {
        static GGCharityCache()
        {
            CacheRules = new CacheRuleManager();
        }
        public static CacheRuleManager CacheRules { get; private set; }
        public static AzureCache Get()
        {
            return new AzureCache(CacheRules, "default");
        }
    }

    public class GGCharityWebDatabase : GGCharityDatabase<GGCharityWebDatabase, BaseController>, IDisposable, IGlobalDataUpdaterDb
    {
        static GGCharityWebDatabase()
        {
            _rules = new EntityRuleManager<BaseController>();
        }

        public static GGCharityWebDatabase Get(BaseController controller)
        {
            return new GGCharityWebDatabase(controller, GGCharityCache.Get());
        }

        protected GGCharityWebDatabase(BaseController requestContext, Cache cache)
            : base(requestContext, cache, _rules)
        {
            _controller = requestContext;
            SetCache(cache);
        }

        private BaseController _controller;
        public override EventManager GetEventManager(DateTime? effectiveTime = null)
        {
            if (effectiveTime == null)
            {
                effectiveTime = GGCharitySession.Get().EffectiveTime;
            }
            return EventManager.Get(this, effectiveTime.Value);
        }

        public PledgeManager GetPledgeManager()
        {
            return PledgeManager.Get(this, GGCharitySession.Get().EffectiveTime);
        }

        public ApplicationUserManager GetUserManager()
        {
            return _controller.HttpContext.GetOwinContext().Get<ApplicationUserManager>();
        }

        public GameManager GetGameManager()
        {
            return GameManager.Get(this);
        }

        private static EntityRuleManager<BaseController> _rules;
        public static EntityRuleManager<BaseController> EntityRules
        {
            get
            {
                return _rules;
            }
        }

        protected override void PrepareOperation()
        {
            if (_controller != null)
            {
                _controller.HttpContext.GetOwinContext().Set<GGCharityDataContext>(this.Context);
                GetUserManager().ResetStore();
            }
        }
    }
}
