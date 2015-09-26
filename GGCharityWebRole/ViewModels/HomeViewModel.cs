using CloudCore.Storage;
using GGCharityCore;
using GGCharityData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Data.Entity;

namespace GGCharityWebRole.ViewModels
{
    public class HomeViewModel : IStorageBackedModel<GGCharityWebDatabase>
    {
        public HomeViewModel()
        {
            Games = new List<Game>();
        }

        public async Task LoadFromStorageAsync(GGCharityWebDatabase Storage)
        {
            Games = await Storage.GetGameManager().GetGameListAsync();
            var Event = await Storage.GetEventManager().GetMainEventAsync();
          
            if (Event != null)
            {
                MainEvent = new EventViewModel(Event.Id, 1);
                await MainEvent.LoadFromStorageAsync(Storage);
            }

            AllEvents = await Storage.Events.ToListAsync();
        }

        public IEnumerable<Game> Games;
        public EventViewModel MainEvent;
        public List<Event> AllEvents;
    }
}