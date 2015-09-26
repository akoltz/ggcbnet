using CloudCore.Storage;
using GGCharityData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Data.Entity;

namespace GGCharityWebRole.ViewModels
{
    public class GameProfileViewModel
    {
        /// <summary>
        /// URL of the player's profile on the respective game's website.  E.g.
        /// the battle.net profile URL.  Can be null if no such profile exists.
        /// </summary>
        public string WebProfileUrl { get; set; }

        public GameID GameId { get; set; }
    }

    public class UserViewModel : IStorageBackedModel<GGCharityWebDatabase>
    {
        public UserViewModel(string Username)
        {
            this.Username = Username;
            this.GameProfiles = new List<GameProfileViewModel>();
        }

        public string Username;
        public Event MainEvent;
        public EventRegistration MainEventRegistration;
        public GGCharityUser User;
        public List<GameProfileViewModel> GameProfiles;
        public Dictionary<GameID, string> GameIconUrls;

        public virtual async Task LoadFromStorageAsync(GGCharityWebDatabase Storage)
        {
            var eventManager = Storage.GetEventManager();
            var userManager = Storage.GetUserManager();
            var gameManager = Storage.GetGameManager();

            User = await userManager.FindByNameAsync(Username);
            GameProfiles = new List<GameProfileViewModel>();
            if (User.StarCraftProfile != null)
            {
                AddStarCraftProfile(User.StarCraftProfile);
            }

            MainEvent = await eventManager.GetMainEventAsync();
            if (MainEvent != null)
            {
                MainEventRegistration = await eventManager.FindEventRegistrationForPlayerAsync(MainEvent, User);
            }

            var gameList = await gameManager.GetGameListAsync();
            GameIconUrls = (from game in gameList select new { game.GameId, game.IconUrl }).ToDictionary(g => g.GameId, g => g.IconUrl);
        }

        private void AddStarCraftProfile(BattleNetApi.StarCraft.Character character)
        {
            GameProfiles.Add(new GameProfileViewModel
            {
                GameId = GameID.StarCraft,
                WebProfileUrl = character.ProfileUrl.ToString()
            });
        }
    }

    public class UserPublicViewModel : UserViewModel
    {
        public UserPublicViewModel(string Id, string ViewerId)
            : base (Id)
        {
            this.ViewerId = ViewerId;
        }

        string ViewerId;
        public Pledge ViewerPledge;
        public StreamingPlayerInfo Stream;
        public override async Task LoadFromStorageAsync(GGCharityWebDatabase Storage)
        {
            await base.LoadFromStorageAsync(Storage);
            if (!String.IsNullOrWhiteSpace(User.StreamUrl))
            {
                var playerStream = await PlayerStream.FromStreamUrlAsync(User.StreamUrl);
                if (playerStream != null)
                {
                    Stream = new StreamingPlayerInfo
                    {
                        Featured = User.IsFeatured,
                        StreamUrl = User.StreamUrl,
                        StreamUserName = playerStream.Username,
                        Username = User.UserName
                    };
                }
            }

            if (MainEventRegistration != null)
            {
                ViewerPledge = await Storage.GetPledgeManager().FindDonorPledgeAsync(ViewerId, User.Id, MainEventRegistration.EventId);
            }
        }
    }

    public class UserPrivateInProgressEventData
    {
        public List<Pledge> MainEventPledgesMade;

        public async Task LoadFromStorageAsync(GGCharityWebDatabase Storage, Event MainEvent, GGCharityUser User)
        {
            string includes = String.Empty;
            if (MainEvent.HasStarted())
            {
                includes = "Receiver";
            }
            MainEventPledgesMade = (await Storage.GetPledgeManager().FindPledgesFromDonorForEventAsync(User.Id, MainEvent.Id, includes)) ?? new List<Pledge>();
        }
    }

    public class UserPrivateCompleteEventData
    {
        public List<IGrouping<Charity, Pledge>> PledgesByCharity;

        public async Task LoadFromStorageAsync(GGCharityWebDatabase Storage, Event MainEvent, GGCharityUser User)
        {
            PledgesByCharity = (await Storage.GetPledgeManager().FindPledgesFromDonorByCharityAsync(User.Id, MainEvent.Id)) ?? new List<IGrouping<Charity, Pledge>>();
        }
    }

    public class UserPrivateViewModel : UserViewModel
    {
        public UserPrivateViewModel(string Id)
            : base (Id)
        {
        }

        public List<Pledge> MainEventPledgesReceived;
        public List<Event> PastEventsWithPledges;
        public UserPrivateInProgressEventData InProgressEventData;
        public UserPrivateCompleteEventData CompleteEventData;

        public override async Task LoadFromStorageAsync(GGCharityWebDatabase Storage)
        {
            await base.LoadFromStorageAsync(Storage).ConfigureAwait(false);

            PastEventsWithPledges = new List<Event>();

            if (MainEvent != null)
            {
                if (MainEvent.GetPhase() != EventPhase.Upcoming)
                {
                    MainEventPledgesReceived = (await Storage.GetPledgeManager().FindPledgesForPlayerAsync(User.Id, MainEvent.Id)) ?? new List<Pledge>();
                }

                if ((MainEvent.GetPhase() == EventPhase.Registration)
                    || (MainEvent.GetPhase() == EventPhase.Live))
                {
                    InProgressEventData = new UserPrivateInProgressEventData();
                    await InProgressEventData.LoadFromStorageAsync(Storage, MainEvent, User).ConfigureAwait(false);
                }
                else if (MainEvent.GetPhase() == EventPhase.Complete)
                {
                    CompleteEventData = new UserPrivateCompleteEventData();
                    await CompleteEventData.LoadFromStorageAsync(Storage, MainEvent, User).ConfigureAwait(false);
                }
            }
        }
    }
}