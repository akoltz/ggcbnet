using GGCharityData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace GGCharityWebRole
{
    public static class UrlHelperExtensions
    {
        public static string EventDetailsLink(this UrlHelper Url, Event e)
        {
            return Url.Action("Details", "Event", new { Id = e.Id, slug = e.GetSlug() });
        }

        public static string PlayerProfileLink(this UrlHelper Url, GGCharityUser user)
        {
            return Url.Action("Index", "User", new { Id = user.UserName });
        }

        public static string PlayerProfilePublicLink(this UrlHelper Url, GGCharityUser user)
        {
            return Url.Action("Public", "User", new { Id = user.UserName });
        }

        public static string PledgeCreateLink(this UrlHelper Url, EventRegistration receiver)
        {
            return Url.Action("Create", "Pledge", new { EventId = receiver.EventId, username = receiver.User.UserName });
        }

        public static string PledgeDeleteLink(this UrlHelper Url, Pledge pledge, string returnUrl = null)
        {
            return Url.Action("Delete", "Pledge", new { EventId = pledge.EventId, donor = pledge.Donor.UserName, receiver = pledge.Receiver.User.UserName, returnUrl = returnUrl });
        }
        public static string PledgeDeleteLink(this UrlHelper Url, dynamic pledge, string returnUrl = null)
        {
            return Url.Action("Delete", "Pledge", new { EventId = pledge.EventId, donor = pledge.Donor.UserName, receiver = pledge.Receiver.User.UserName, returnUrl = returnUrl });
        }
    }
}