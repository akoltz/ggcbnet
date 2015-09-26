using GGCharityCore;
using GGCharityData;
using Microsoft.AspNet.Identity;
using Microsoft.WindowsAzure.ServiceRuntime;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Data.Entity.Migrations;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using System.Security.Claims;
using LogCore;
using MatchHistoryStorage;
using MatchResultsProvider;
using System.Globalization;

namespace GGCharityWebRole
{
    public class DbInitializer : IDatabaseInitializer<GGCharityDataContext>
    {
        public void InitializeDatabase(GGCharityDataContext context)
        {
            var migrationConfiguration = new GGCharityData.Migrations.Configuration(Config.Get().InDevelopmentEnvironment);

            FailFastOnDangerousMigrationSettings(migrationConfiguration);

            MigrateDatabaseToLatestVersion<GGCharityDataContext, GGCharityData.Migrations.Configuration> migrate
                = new MigrateDatabaseToLatestVersion<GGCharityDataContext, GGCharityData.Migrations.Configuration>(true, migrationConfiguration);

            migrate.InitializeDatabase(context);
        }

        private static void FailFastOnDangerousMigrationSettings(GGCharityData.Migrations.Configuration migrationConfiguration)
        {
            if (!Config.Get().InDevelopmentEnvironment)
            {
                if (migrationConfiguration.AutomaticMigrationsEnabled || migrationConfiguration.AutomaticMigrationDataLossAllowed)
                {
                    Environment.FailFast("Dangerous auto migration settings were detected!");
                }
            }
        }

        private static TraceSource Log;

        static DbInitializer()
        {
            Log = Logging.GetLongLivedLog("DbInitializier", "DbInitializer");
        }

        public static async Task SeedDB(GGCharityWebDatabase context)
        {
            // Redundant but let's not screw this up...
            if (!Config.Get().InDevelopmentEnvironment
                || !RoleEnvironment.IsEmulated
                || !RoleEnvironment.IsAvailable)
            {
                Log.TraceEvent(TraceEventType.Critical, 0, "Cannot run Seed outside of the development environment");
                throw new InvalidOperationException("Cannot run Seed outside of the development environment");
            }

            Log.TraceEvent(TraceEventType.Information, 0, "Seeding Database because model changed");
            Log.TraceEvent(TraceEventType.Start, 0, "Seeding Database because model changed");

            await SeedAccounts(context);
            await TestServices.Games.SeedGames(context);
            await TestServices.Charities.SeedCharities(context);
            await SeedEvents(context);
            await SeedPledges(context);

            Log.TraceEvent(TraceEventType.Stop, 0, "Seeding Database because model changed");
        }

        private static async Task SeedEvents(GGCharityWebDatabase context)
        {
            await TestServices.Events.SeedEventAndRegistrationsAsync(context, "Tanksgiving", Log, DateTime.UtcNow).ConfigureAwait(false);
            await TestServices.Events.SeedEventAndRegistrationsAsync(context, "Future Event", Log, DateTime.UtcNow + TimeSpan.FromDays(7)).ConfigureAwait(false);
            await TestServices.Events.SeedEventAndRegistrationsAsync(context, "Future Event #2", Log, DateTime.UtcNow + TimeSpan.FromDays(14)).ConfigureAwait(false);
        }


        private static async Task SeedPledges(GGCharityWebDatabase data)
        {
            await TestServices.Pledges.SeedPledgesAsync(data, "Tanksgiving").ConfigureAwait(false);
            await TestServices.Pledges.SeedPledgesAsync(data, "Future Event").ConfigureAwait(false);
            await TestServices.Pledges.SeedPledgesAsync(data, "Future Event #2").ConfigureAwait(false);
        }

        private static async Task SeedAccounts(GGCharityWebDatabase data)
        {
            await data.PerformAsync(async () => 
            { 
                var manager = new ApplicationUserManager(new UserStore<GGCharityUser>(data.RawContext));
                await CreateSeedAccount(manager, "Bey", "ggsc2email@gmail.com", true);
                await CreateSeedAccount(manager, "imaznation", "alex@imaznation.com", true);

                await AddStarCraftLogin(manager, "Bey", "409385", TestServices.StarCraftJson.BeyJson);
                await AddStarCraftLogin(manager, "imaznation", "123456", TestServices.StarCraftJson.ImaznationJson);

                await data.SaveChangesAsync();
            });

            await TestServices.Users.SeedUsers(data).ConfigureAwait(false);
        }

        private static async Task AddStarCraftLogin(ApplicationUserManager manager, string accountName, string accessToken, string json)
        {
            var user = await manager.FindByNameAsync(accountName);
            var loginInfo = new UserLoginInfo("BattleNet", accessToken);
            var loginResult = await manager.AddLoginAsync(user.Id, loginInfo);
            if (!loginResult.Succeeded)
            {
                throw new InvalidOperationException();
            }

            user.GameProfiles.Add(new GameProfile
            {
                GameId = GameID.StarCraft,
                ProfileData = json,
                ProfileDataVersion = 1,
                UserId = user.Id,
                AdditionalData = BattleNetApi.Region.US.ToString()
            });
        }

        private static async Task<IdentityResult> CreateSeedAccount(ApplicationUserManager manager, string username, string email, bool featured)
        {
            var user = new GGCharityUser() { UserName = username, Email = email, IsFeatured = featured, TimeZoneId = "Pacific Standard Time" };
            return await manager.CreateAsync(user, Config.Get().TestAccountPassword);
        }
    }

}