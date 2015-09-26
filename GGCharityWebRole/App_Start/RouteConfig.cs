using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace GGCharityWebRole
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "Event",
                url: "Event/{id}/{slug}",
                defaults: new { controller = "Event", action = "Details", slug = UrlParameter.Optional },
                namespaces: new[] { "GGCharityWebRole.Controllers" }
                );

            routes.MapRoute(
                name: "User",
                url: "User/{id}",
                defaults: new { controller = "User", action = "Index", id = UrlParameter.Optional },
                namespaces: new[] { "GGCharityWebRole.Controllers" }
                );

            routes.MapRoute(
                name: "PledgeCreate",
                url: "Pledge/Create/{eventId}/{username}",
                defaults: new { controller = "Pledge", action = "Create" },
                namespaces: new[] { "GGCharityWebRole.Controllers" }
                );

            routes.MapRoute(
                name: "PledgeDelete",
                url: "Pledge/Delete/{eventId}/{username}",
                defaults: new { controller = "Pledge", action = "Create" },
                namespaces: new[] { "GGCharityWebRole.Controllers" }
                );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional },
                namespaces: new [] { "GGCharityWebRole.Controllers" }
            );
        }
    }
}
