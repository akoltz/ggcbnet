using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.DataProtection;
using Microsoft.Owin.Security.Google;
using Owin.Security.Providers.BattleNet;
using Owin;
using System;
using GGCharityWebRole.Models;
using GGCharityData;
using GGCharityCore;

namespace GGCharityWebRole
{


    public partial class Startup
    {
        // For more information on configuring authentication, please visit http://go.microsoft.com/fwlink/?LinkId=301864
        public void ConfigureAuth(IAppBuilder app)
        {
            // Configure the db context and user manager to use a single instance per request.
            //app.CreatePerOwinContext();
            app.CreatePerOwinContext<ApplicationUserManager>(ApplicationUserManager.Create);
            
            // Enable the application to use a cookie to store information for the signed in user
            // and to use a cookie to temporarily store information about a user logging in with a third party login provider
            // Configure the sign in cookie
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                LoginPath = new PathString("/Account/Login"),
                Provider = new CookieAuthenticationProvider
                {
                    OnValidateIdentity = SecurityStampValidator.OnValidateIdentity<ApplicationUserManager, GGCharityUser>(
                        validateInterval: TimeSpan.FromMinutes(30),
                        regenerateIdentity: (manager, user) => user.GenerateUserIdentityAsync(manager))
                }
            });
            
            app.UseExternalSignInCookie(DefaultAuthenticationTypes.ExternalCookie);

            // Uncomment the following lines to enable logging in with third party login providers
            //app.UseMicrosoftAccountAuthentication(
            //    clientId: "",
            //    clientSecret: "");

            //app.UseTwitterAuthentication(
            //   consumerKey: "",
            //   consumerSecret: "");

            //app.UseFacebookAuthentication(
            //   appId: "",
            //   appSecret: "");

            //app.UseGoogleAuthentication(new GoogleOAuth2AuthenticationOptions()
            //{
            //    ClientId = Config.Get().GoogleAccountClientId,
            //    ClientSecret = Config.Get().GoogleAccountClientSecret
            //});

            var battleNetAuth = new BattleNetAuthenticationOptions()
            {
                ClientId = Config.Get().BattleNetClientId,
                ClientSecret = Config.Get().BattleNetClientSecret,
                Region = Region.US,
            };

            battleNetAuth.Scope.Clear();
            battleNetAuth.Scope.Add("sc2.profile");
            battleNetAuth.CallbackPath = new PathString(@"/signin-bnet");

            app.UseBattleNetAuthentication(battleNetAuth);

            //app.Use(typeof(BattleNetAuthenticationMiddleware), app, new BattleNetAuthenticationOptions()
            //{
            //    ClientId = Config.Get().BattleNetClientId,
            //    ClientSecret = Config.Get().BattleNetClientSecret
            //});
        }
    }
}