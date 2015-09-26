using GGCharityData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GGCharityWebRole
{
    public static class ModelExtensions
    {
        public static Pledge PledgeToPlayer(this GGCharityUser donor, EventRegistration receiver, Decimal amount)
        {
            if (!receiver.CanPlayerAcceptPledges)
            {
                throw new InvalidOperationException("Receiver is not ready to accept pledges");
            }

            var pledge = new Pledge(receiver, donor, amount);

            if (donor.Pledges == null)
            {
                donor.Pledges = new List<Pledge>();
            }

            if(receiver.PledgesReceived == null)
            {
                receiver.PledgesReceived = new List<Pledge>();
            }
            donor.Pledges.Add(pledge);
            receiver.PledgesReceived.Add(pledge);
            return pledge;
        }

        public static EventPhase GetPhase(this Event Event)
        {
            return Event.GetPhase(GGCharitySession.Get().EffectiveTime);
        }

        public static bool HasStarted(this Event Event)
        {
            return Event.HasStarted(GGCharitySession.Get().EffectiveTime);
        }

        public static string GetSlug(this Event Event)
        {
            string gameShortName;
            if (Event.Game.GameId == GameID.StarCraft)
            {
                gameShortName = "starcraft";
            }
            else
            {
                throw new NotImplementedException();
            }
            return String.Format("{0}-{1}", gameShortName, Event.Name.ToLowerInvariant().Replace(" ", "-"));
        }
    }
}