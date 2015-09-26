using GGCharityCore;
using GGCharityData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GGCharityWebRole
{
    public class GGCharitySession
    {
        #region Factory methods and members
        private static GGCharitySession _Instance;

        static GGCharitySession()
        {
            _Instance = new GGCharitySession();
        }
        public static GGCharitySession Get()
        {
            return _Instance;
        }
        #endregion

        public String AnonymousId
        {
            get
            {
                return (string)HttpContext.Current.Session["AnonymousId"];
            }
            private set
            {
                HttpContext.Current.Session["AnonymousId"] = value;
            }
        }

        public bool EnableDebugPanel
        {
            get
            {
                object result = HttpContext.Current.Session["EnableDebugPanel"];
                return (result == null) ? false : (bool)result;
            }
            set
            {
                HttpContext.Current.Session["EnableDebugPanel"] = value;
            }
        }

        public DateTime EffectiveTime
        {
            get
            {
                object result = (HttpContext.Current != null) ? HttpContext.Current.Session["EffectiveTime"] : null;
                return (result == null) ? DateTime.UtcNow : (DateTime)result;
            }
            set
            {
                HttpContext.Current.Session["EffectiveTime"] = value;
            }
        }

        public TimeZoneInfo TimeZone
        {
            get
            {
                object result = HttpContext.Current.Session["TimeZone"];
                return (result == null) ? TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time") : TimeZoneInfo.FindSystemTimeZoneById((string)result);
            }
            set
            {
                HttpContext.Current.Session["TimeZone"] = value.Id;
            }
        }

        internal void Initialize(GGCharityUser user)
        {
            AnonymousId = user.Id;
            if (Config.Get().InDevelopmentEnvironment)
            {
                EnableDebugPanel = true;
            }

            try
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZoneId ?? "Pacific Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            }
        }

        internal void Abandon()
        {
            HttpContext.Current.Session.Abandon();
        }

        /// <summary>
        /// Reset is used when the session state needs to be cleared but the session itself maintained.
        /// This is useful primarily in development when using the "Login As" buttons.  If the session
        /// is abandoned when the first logout happens, then the subsequent login is also thrown away.
        /// </summary>
        internal void Reset()
        {
            HttpContext.Current.Session.Clear();
        }
    }
}