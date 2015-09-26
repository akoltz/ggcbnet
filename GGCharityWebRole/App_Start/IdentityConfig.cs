using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using GGCharityWebRole.Models;
using GGCharityData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GGCharityWebRole
{
    // Configure the application user manager used in this application. UserManager is defined in ASP.NET Identity and is used by the application.

    public class ApplicationUserManager : UserManager<GGCharityUser>
    {
        public ApplicationUserManager(IUserStore<GGCharityUser> store)
            : base(store)
        {
        }

        private IOwinContext context;
        public void ResetStore()
        {
            Store = new UserStore<GGCharityUser>(context.Get<GGCharityDataContext>());
        }

        public static ApplicationUserManager Create(IdentityFactoryOptions<ApplicationUserManager> options, IOwinContext context)
        {
            // Stand up an application manager with a dummy db context.
            // The real dbcontext will be inserted inside a Dboperation with the ResetStore
            // method.  
            var manager = new ApplicationUserManager(new UserStore<GGCharityUser>(GGCharityDataContext.Get()));
            // Configure validation logic for usernames
            InitUserManager(options, manager);
            manager.context = context;
            return manager;
        }

        private static void InitUserManager(IdentityFactoryOptions<ApplicationUserManager> options, ApplicationUserManager manager)
        {
            manager.UserValidator = new UserValidator<GGCharityUser>(manager)
            {
                AllowOnlyAlphanumericUserNames = false,
                RequireUniqueEmail = true
            };
            // Configure validation logic for passwords
            manager.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 6,
                RequireNonLetterOrDigit = false,
                RequireDigit = false,
                RequireLowercase = false,
                RequireUppercase = false
            };
            // Register two factor authentication providers. This application uses Phone and Emails as a step of receiving a code for verifying the user
            // You can write your own provider and plug in here.
            //manager.RegisterTwoFactorProvider("PhoneCode", new PhoneNumberTokenProvider<ApplicationUser>
            //{
            //    MessageFormat = "Your security code is: {0}"
            //});
            //manager.RegisterTwoFactorProvider("EmailCode", new EmailTokenProvider<ApplicationUser>
            //{
            //    Subject = "Security Code",
            //    BodyFormat = "Your security code is: {0}"
            //});
            manager.EmailService = new EmailService();
            manager.SmsService = new SmsService();
            var dataProtectionProvider = options.DataProtectionProvider;
            if (dataProtectionProvider != null)
            {
                manager.UserTokenProvider = new DataProtectorTokenProvider<GGCharityUser>(dataProtectionProvider.Create("ASP.NET Identity"))
                {
                    TokenLifespan = TimeSpan.FromHours(72)
                };
            }
        }

        public override async Task<GGCharityUser> FindByNameAsync(string userName)
        {
            var user = await base.FindByNameAsync(userName);
            return (user != null && user.IsAccountLocked) ? null : user;
        }

        public override async Task<GGCharityUser> FindAsync(string userName, string password)
        {
            var user = await base.FindAsync(userName, password);
            return (user != null && user.IsAccountLocked) ? null : user;
        }

        public override async Task<GGCharityUser> FindAsync(UserLoginInfo login)
        {
            var user = await base.FindAsync(login);
            return (user != null && user.IsAccountLocked) ? null : user;
        }

        public override async Task<GGCharityUser> FindByEmailAsync(string email)
        {
            var user = await base.FindByEmailAsync(email);
            return (user != null && user.IsAccountLocked) ? null : user;
        }

        public override async Task<GGCharityUser> FindByIdAsync(string userId)
        {
            var user = await base.FindByIdAsync(userId);
            return (user != null && user.IsAccountLocked) ? null : user;
        }

        public Task<GGCharityUser> FindByIdIgnoreAccountLockAsync(string userId)
        {
            return base.FindByIdAsync(userId);
        }
    }

    public class EmailService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message)
        {
            return GGCharityCore.Email.Get().SendIdentityMessageAsync(message);
        }
    }

    public class SmsService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message)
        {
            // Plug in your sms service here to send a text message.
            return Task.FromResult(0);
        }
    }
}
