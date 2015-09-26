using CloudCore.Storage;
using GGCharityData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GGCharityWebRole.ViewModels
{
    public class PledgeTemplate
    {
        public Decimal Amount;
        public string UserName;
        public int EventId;
    }

    public class PledgeViewModel : IStorageBackedModel<GGCharityWebDatabase>
    {
        public PledgeViewModel()
        {
            User = new GGCharityUser();
        }
        
        public PledgeViewModel(string username, int EventId)
        {
            // TODO: Complete member initialization
            this.UserName = username;
            this.EventId = EventId;
        }
        public GGCharityUser User { get; private set; }
        public Event Event { get; private set; }
        public EventRegistration UserRegistration { get; private set; }
        public Decimal Amount { get; set; }
        public string UserName { get; set; }
        public int EventId { get; set; }
        
        /// <summary>
        /// This field is referenced by the view to create an amount edit box.
        /// </summary>
        public Decimal AmountPerWin { get; set; }

        public async System.Threading.Tasks.Task LoadFromStorageAsync(GGCharityWebDatabase Storage)
        {
            await Storage.PerformAsync(async () =>
            {
                var user = await Storage.GetUserManager().FindByNameAsync(UserName).ConfigureAwait(false);
                var Event = await Storage.GetEventManager().FindEventByIdAsync(EventId).ConfigureAwait(false);

                this.User = user;
                this.Event = Event;
                this.UserRegistration = await Storage.GetEventManager().FindEventRegistrationForPlayerAsync(Event, User).ConfigureAwait(false);
            });
        }
    }

    public class PledgeControlViewModel
    {
        public List<Pledge> Pledges;
        /// <summary>
        /// True if the pledges in this list are for the viewer, false if they are FROM the viewer to other players.
        /// </summary>
        public bool ArePledgesForViewer;
        public EventPhase EventPhase;
    }
}