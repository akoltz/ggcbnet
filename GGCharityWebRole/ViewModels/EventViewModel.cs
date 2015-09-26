using CloudCore.Storage;
using GGCharityData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Data.Entity;
using GGCharityCore;

namespace GGCharityWebRole.ViewModels
{
    public class StreamingPlayerInfo
    {
        public string Username;
        public bool Featured;
        public string StreamUrl;
        public string StreamUserName;
    }

    public class LiveEventData
    {
        public LiveEventData()
        {
        }

        public async Task LoadFromEventAsync(Event Event, GGCharityWebDatabase Storage)
        {
            TotalWinsGoal = Event.Registrations.Sum(p => p.WinsGoal);
            TotalWinsAchieved = Event.Registrations.Sum(p => p.WinsAchieved);
            TotalPledgesRaised = Event.Registrations.Sum(p => p.PledgesReceived.Sum(m => m.AmountPerWin * p.WinsAchieved));

            // The background worker that retreieves this data has no concept of 
            // a session or effective time, which makes development a little tricky.
            // So, we'll request a new list on each page load in that case.
            //
            // In prod, the background worker keeps a list in our cache for quick retrieval.
            if (Config.Get().UseTestStreams)
            {
                await Storage.PerformAsync(async () =>
                {
                    StreamingPlayers = await GGCharityWebRole.StreamingPlayersUpdater.GetRefreshedData(Storage, GGCharitySession.Get().EffectiveTime);
                });
            }
            else
            {
                StreamingPlayers = GGCharityWebRole.StreamingPlayersUpdater.GetStreamingPlayers();
            }
        }

        public int TotalWinsGoal { get; private set; }
        public int TotalWinsAchieved { get; private set; }
        public Decimal TotalPledgesRaised { get; private set; }

        public IEnumerable<StreamingPlayerInfo> StreamingPlayers { get; private set; }
    }
    public class EventViewModel : IStorageBackedModel<GGCharityWebDatabase>
    {
        public const int PlayerPageSize = 4;
        public int Id { get; private set; }
        public Event Event { get; private set; }
        public Decimal TotalPledged { get; private set; }

        public int PlayerPage { get; private set; }
        public List<EventRegistration> Players { get; private set; }
        public int TotalPlayersRegistered { get; private set; }
        public LiveEventData LiveEventData { get; private set; }
        public EventViewModel(int Id, int PlayerPage)
        {
            this.Id = Id;
            this.PlayerPage = PlayerPage;
        }

        public async Task LoadFromStorageAsync(GGCharityWebDatabase Storage)
        {
            Event = await (from e in Storage.Events 
                           where e.Id == this.Id 
                           select e).SingleAsync();

            TotalPledged = (from p in Event.Registrations 
                            select p.PledgesReceived.Sum(m => m.PledgeTimeWinsGoal * m.Amount)).Sum();

            TotalPlayersRegistered = Event.Registrations.Count;

            Players = (from p in Event.Registrations 
                       where !p.User.IsAccountLocked
                       select p).Skip((PlayerPage - 1) * PlayerPageSize)
                                .Take(PlayerPageSize)
                                .ToList();

            if (Event.GetPhase() == EventPhase.Live
                || Event.GetPhase() == EventPhase.Complete)
            {
                LiveEventData = new LiveEventData();
                await LiveEventData.LoadFromEventAsync(Event, Storage);
            }
        }
    }
}