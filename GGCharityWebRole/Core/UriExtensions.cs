using Microsoft.WindowsAzure.ServiceRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GGCharityWebRole
{
    internal static class UriExtensions
    {
        /// <summary>
        /// Returns a request URL that is automatically adjusted for the azure emulation environment.
        /// </summary>
        /// <param name="request">The request from which the URL should be retrieved.</param>
        /// <returns>The requested URL, with adjustments for emulation if necessary</returns>
        /// <remarks>
        /// In azure emulation, ports 8080 are used by the azure service and it forwards to 8081
        /// on which IIS actually listens.  The URL is to 8080, but IIS will see a request to 8081.
        /// If we try to return to the requested URL at 8081, we'll fail.  The same is true of SSL
        /// ports 443 and 444.
        /// </remarks>
        public static Uri GetEmulationAwareUrl(this HttpRequestBase request)
        {
            Uri uri;
            if (RoleEnvironment.IsEmulated)
            {
                UriBuilder builder = new UriBuilder(request.Url);
                builder.Port -= 1;
                uri = builder.Uri;
            }
            else
            {
                uri = request.Url;
            }
            return uri;
        }

        public static Uri GetEmulationAwareUrl(string urlString)
        {
            UriBuilder builder = new UriBuilder(urlString);
            if (RoleEnvironment.IsEmulated)
            {
                builder.Port -= 1;
            }
            return builder.Uri;
        }
    }
}