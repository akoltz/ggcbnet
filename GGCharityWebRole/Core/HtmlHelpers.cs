using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GGCharityWebRole
{
    public static class HtmlHelpers
    {
        public static string TimeInUserTimeZone(this System.Web.Mvc.HtmlHelper html, DateTime dateTimeUtc)
        {
            var timezone = GGCharitySession.Get().TimeZone;
            return (TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, timezone).ToString());
        }
    }
}