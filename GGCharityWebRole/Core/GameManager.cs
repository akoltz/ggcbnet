using CloudCore.Storage;
using GGCharityData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Data.Entity;

namespace GGCharityWebRole
{
    public class GameManager
    {
        public class GameList : ICacheableModel<GGCharityWebDatabase>
        {
            public List<Game> Games;

            public bool OnLoadFromCache(GGCharityWebDatabase Storage)
            {
                /// If we retrived the object from the cache, attach the entities
                /// to the data context.  Otherwise, if any new entities reference
                /// these games, the game will also be treated as new.
                foreach(var game in Games)
                {
                    Storage.RawContext.Entry(game).State = EntityState.Unchanged;
                }
                return true;
            }

            public string GetCacheKey()
            {
                return GameListCacheKey;
            }

            public async Task LoadFromStorageAsync(GGCharityWebDatabase Storage)
            {
                Games = await Storage.RawContext.Games.ToListAsync().ConfigureAwait(false);
            }
        }

        private GGCharityWebDatabase data;
        private const string GameListCacheKey = "GameManager_GameList";

        public GameManager(GGCharityWebDatabase data)
        {
            // TODO: Complete member initialization
            this.data = data;
        }

        static GameManager()
        {
            GGCharityCache.CacheRules.AddEntityChangedRule<Game>((game, state, values, keysToInvalidate) =>
            {
                keysToInvalidate.Add(GameListCacheKey);
            });
        }

        public static GameManager Get(GGCharityWebDatabase data)
        {
            return new GameManager(data);
        }

        public async Task<IEnumerable<Game>> GetGameListAsync()
        {
            var gameList = await data.GetModelAsync<GameList>(() => { return new GameList(); }).ConfigureAwait(false);
            return gameList.Games;
        }

        internal void InvalidateCachedGames()
        {
            GGCharityCache.Get().Invalidate(GameListCacheKey);
        }
    }
}