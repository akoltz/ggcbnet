using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Owin;
using GGCharityWebRole.Models;
using GGCharityData;
using GGCharityCore;
using System.Net;
using System.Diagnostics;

namespace GGCharityWebRole.Controllers
{
    [Authorize]
    [RequireHttps]
    public class AccountController : BaseController
    {
        public AccountController()
            : base("AccountController")
        {
        }

        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            Trace.TraceInformation("Beginning login for {0}, returnUrl {1}", model.UsernameOrEmail, returnUrl);
            return await Data.PerformAsync(async () =>
            {
                if (ModelState.IsValid)
                {
                    var user = await UserManager.FindAsync(model.UsernameOrEmail, model.Password);
                    if (user != null)
                    {
                        Trace.TraceInformation("Found user by name {0}, completing login", model.UsernameOrEmail);
                        await SignInAsync(user, model.RememberMe);
                        return RedirectToLocal(returnUrl);
                    }
                    else
                    {
                        Trace.TraceInformation("Failed to find user by name {0}, trying email", model.UsernameOrEmail);
                        user = await UserManager.FindByEmailAsync(model.UsernameOrEmail);
                        if (user != null)
                        {
                            Trace.TraceInformation("Found user by email {0}, completing login", model.UsernameOrEmail);
                            user = await UserManager.FindAsync(user.UserName, model.Password);
                            if (user != null)
                            {
                                await SignInAsync(user, model.RememberMe);
                                return RedirectToLocal(returnUrl);
                            }
                        }
                        Trace.TraceEvent(TraceEventType.Error, 0, "Failed to find user {0}, username or password was incorrect", model.UsernameOrEmail);
                        ModelState.AddModelError("", "Invalid username or password.");
                    }
                }

                Trace.TraceEvent(TraceEventType.Error, 0, "Model state was not valid for user {0}'s login attempt.  {1}", model.UsernameOrEmail, String.Join(", ", ModelState));

                // If we got this far, something failed, redisplay form
                return View(model);
            });
        }

        //
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new RegisterViewModel());
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            Trace.TraceInformation("Handling register request for user {0}, timezone {1}, returnUrl = {2}", model.Username, model.TimeZoneId, model.ReturnUrl);
            if (ModelState.IsValid)
            {
                var user = new GGCharityUser() { UserName = model.Username, Email = model.Email, TimeZoneId = model.TimeZoneId };
                IdentityResult result = null;
                await Data.PerformAsync(async () =>
                {
                    result = await UserManager.CreateAsync(user, model.Password);
                });

                if (result.Succeeded)
                {
                    return await PostRegistrationSignIn(model.ReturnUrl, user);
                }
                else
                {
                    AddErrors(result);
                }
            }

            Trace.TraceEvent(TraceEventType.Error, 0, "Model state invalid for registration attempt for user {0}.  Error = {1}", model.Username, String.Join(", ", ModelState));

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/ConfirmEmail
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail(string userId, string code, string returnUrl)
        {
            Trace.TraceInformation("Confirming email for user {0}", userId);
            if (userId == null || code == null) 
            {
                Trace.TraceEvent(TraceEventType.Critical, 0, "Request to confirm email contained an empty userId or code: User = {0}, code={1}", userId, code);
                return View("Error");
            }

            IdentityResult result = null;
            await Data.PerformAsync(async () =>
            {
                result = await UserManager.ConfirmEmailAsync(userId, code);
            });

            if (result.Succeeded)
            {
                if (returnUrl != null)
                {
                    return RedirectToLocal(returnUrl);
                }
                return View("ConfirmEmail");
            }
            else
            {
                Trace.TraceEvent(TraceEventType.Error, 0, "Failed to confirm email for user {0}", userId);
                AddErrors(result);
                return View();
            }
        }

        //
        // GET: /Account/ForgotPassword
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        //
        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                bool succeeded = false;
                await Data.PerformAsync(async () =>
                {
                    var user = await UserManager.FindByNameAsync(model.Email);

                    if (user == null || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
                    {
                        Trace.TraceEvent(TraceEventType.Error, 0, "User not found or email has not been confirmed, ForgotPassword request has failed");
                        ModelState.AddModelError("", "The user either does not exist or is not confirmed.");
                    }
                    else
                    {
                        // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771
                        // Send an email with this link
                        string code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                        var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                        await UserManager.SendEmailAsync(user.Id, "Reset your GGCharity Password", "Hi " + user.UserName + "!<br /><br />We received a request to reset your password.  If you did not send this request, please ignore this email!  <a href=\"" + callbackUrl + "\">Click here</a> to get started with your password reset.<br /><br />-The GGCharity Team");
                        succeeded = true;
                    }
                });

                if (succeeded)
                {
                    return RedirectToAction("ForgotPasswordConfirmation", "Account");
                }
            }

            Trace.TraceEvent(TraceEventType.Error, 0, "Model state for ForgotPassword request was invalid");

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            return View();
        }
	
        //
        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string code)
        {
            if (code == null)
            {
                return View("Error");
            }
            return View(new ResetPasswordViewModel());
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                IdentityResult result = null;
                await Data.PerformAsync(async () =>
                {
                    var user = await UserManager.FindByNameAsync(model.Email);
                    if (user == null)
                    {
                        ModelState.AddModelError("", "No user found.");
                    }
                    result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
                });

                if (result.Succeeded)
                {
                    return RedirectToAction("ResetPasswordConfirmation", "Account");
                }
                else
                {
                    AddErrors(result);
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult> SwitchAccounts(string username, string returnUrl)
        {
            if (!Config.Get().InDevelopmentEnvironment)
            {
                throw new HttpException((int)HttpStatusCode.Forbidden, "This operation is forbidden");
            }

            LogOffCommon(false);
            await Data.PerformAsync(async () =>
            {
                var user = await UserManager.FindByNameAsync(username);
                await SignInAsync(user, isPersistent: false);
            });
            return RedirectToLocal(returnUrl);
        }

        //
        // GET: /Account/ResetPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        //
        // POST: /Account/Disassociate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Disassociate(string loginProvider, string providerKey)
        {
            IdentityResult result = null;
            await Data.PerformAsync(async () => 
            { 
                result = await UserManager.RemoveLoginAsync(User.Identity.GetUserId(), new UserLoginInfo(loginProvider, providerKey));
            }); 

            if (result.Succeeded)
            {
                return await Data.PerformAsync(async () =>
                {
                    var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                    await SignInAsync(user, isPersistent: false);

                    return RedirectToManageWithMessage(ManageMessageId.RemoveLoginSuccess);
                });
            }
            else
            {
                return RedirectToManageWithMessage(result.Errors.FirstOrDefault());
            }
        }

        public async Task<ActionResult> AddGameAccount(GameID GameId, string returnUrl)
        {
            if (GameId != GGCharityData.GameID.StarCraft)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotImplemented);
            }

            return await Data.PerformAsync<ActionResult>(async () =>
            {
                var currentUser = await GetCurrentUserAsync();
                if (currentUser.HasGame(GameId))
                {
                    return Redirect(returnUrl);
                }

                var gameList = await Data.GetGameManager().GetGameListAsync();
                var game = gameList.SingleOrDefault(g => g.GameId == GameId);
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.GameName = game.Name;
                ViewBag.GameId = GameId;
                ViewBag.ReturnUrl = returnUrl;
                return View("AddGameAccount");
            });
        }

        //
        // GET: /Account/Manage
        public ActionResult Manage()
        {
            ManageMessageId? message = (ManageMessageId?)TempData["ManageMessage"];

            ViewBag.StatusMessage =
                  message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
                : message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
                : message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
                : message == ManageMessageId.Error ? ((string)(TempData["ManageMessageString"] ?? "An unspecified error has occurred"))
                : "";

            ViewBag.HasLocalPassword = HasPassword();
            ViewBag.ReturnUrl = Url.Action("Manage");
            return View();
        }

        //
        // POST: /Account/Manage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Manage(ManageUserViewModel model)
        {
            bool hasPassword = HasPassword();
            ViewBag.HasLocalPassword = hasPassword;
            ViewBag.ReturnUrl = Url.Action("Manage");
            if (hasPassword)
            {
                if (ModelState.IsValid)
                {
                    IdentityResult result = null;

                    await Data.PerformAsync(async () =>
                    {
                        result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword, model.NewPassword);
                    });

                    if (result.Succeeded)
                    {
                        await Data.PerformAsync(async () =>
                        {
                            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                            await SignInAsync(user, isPersistent: false);
                        });
                        return RedirectToManageWithMessage(ManageMessageId.ChangePasswordSuccess);
                    }
                    else
                    {
                        AddErrors(result);
                    }
                }
            }
            else
            {
                // User does not have a password so remove any validation errors caused by a missing OldPassword field
                ModelState state = ModelState["OldPassword"];
                if (state != null)
                {
                    state.Errors.Clear();
                }

                if (ModelState.IsValid)
                {
                    IdentityResult result = await UserManager.AddPasswordAsync(User.Identity.GetUserId(), model.NewPassword);
                    if (result.Succeeded)
                    {
                        return RedirectToManageWithMessage(ManageMessageId.SetPasswordSuccess);
                    }
                    else
                    {
                        AddErrors(result);
                    }
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // POST: /Account/ExternalLogin
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            Trace.TraceInformation("Beginning external login attempt for provider {0}, return URL = {1}", provider, returnUrl);
            ControllerContext.HttpContext.Session.RemoveAll();
            // Request a redirect to the external login provider
            return new ChallengeResult(provider, Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl }));
        }

        //
        // GET: /Account/ExternalLoginCallback
        [AllowAnonymous]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            Trace.TraceInformation("Received external login callback, return URL = {0}", returnUrl);

            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
            if (loginInfo == null)
            {
                Trace.TraceEvent(TraceEventType.Error, 0, "External login attempt failed.  GetExternalLoginInfoAsync returned null");
                return RedirectToAction("Login");
            }

            Trace.TraceInformation("External login proceeding for provider {0}:{1}", loginInfo.Login.LoginProvider, loginInfo.Login.ProviderKey);

            // Sign in the user with this external login provider if the user already has a login
            GGCharityUser user = null;
            await Data.PerformAsync(async () =>
            {
                user = await UserManager.FindAsync(loginInfo.Login);
            });

            if (user == null)
            {
                Trace.TraceInformation("User {0}:{1} does not have an account, beginning ExternalLoginConfirmation flow", loginInfo.Login.LoginProvider, loginInfo.Login.ProviderKey);

                // If the user does not have an account, then prompt the user to create an account
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.LoginProvider = loginInfo.Login.LoginProvider;
                return View("ExternalLoginConfirmation", new ExternalLoginConfirmationViewModel { Email = loginInfo.Email });
            }
            else
            {
                Trace.TraceInformation("External login proceeding for user {0}:{1}, local username {2}", loginInfo.Login.LoginProvider, loginInfo.Login.ProviderKey, user.UserName);
                IList<Claim> userClaims = null;

                await Data.PerformAsync(async () =>
                {
                    await SignInAsync(user, isPersistent: false);
                });

                Trace.TraceInformation("Signin succeeded for user {0}:{1}, local username {2}", loginInfo.Login.LoginProvider, loginInfo.Login.ProviderKey, user.UserName);

                await Data.PerformAsync(async () =>
                {
                    userClaims = await UserManager.GetClaimsAsync(user.Id);
                });

                Trace.TraceInformation("Claims retrieval succeeded for user {0}:{1}, local username {2}", loginInfo.Login.LoginProvider, loginInfo.Login.ProviderKey, user.UserName);

                foreach (var claim in loginInfo.ExternalIdentity.Claims)
                {
                    var existingClaim = userClaims.FirstOrDefault(c => c.Type.Equals(claim.Type));
                    if (existingClaim != null)
                    {
                        Trace.TraceInformation("Removing existing claim {3} for user {0}:{1}, local username {2}", loginInfo.Login.LoginProvider, loginInfo.Login.ProviderKey, user.UserName, claim.ToString());
                        
                        await Data.PerformAsync(async () =>
                        {
                            await UserManager.RemoveClaimAsync(user.Id, existingClaim);
                        });

                        Trace.TraceInformation("Successfully removed existing claim {3} for user {0}:{1}, local username {2}", loginInfo.Login.LoginProvider, loginInfo.Login.ProviderKey, user.UserName, claim.ToString());
                    }

                    if (!claim.Type.Equals(ClaimTypes.NameIdentifier))
                    {
                        Trace.TraceInformation("Adding new claim {3} for user {0}:{1}, local username {2}", loginInfo.Login.LoginProvider, loginInfo.Login.ProviderKey, user.UserName, claim.ToString());
                    
                        await Data.PerformAsync(async () =>
                        {
                            await UserManager.AddClaimAsync(user.Id, claim);
                        });

                        Trace.TraceInformation("Successfully added new claim {3} for user {0}:{1}, local username {2}", loginInfo.Login.LoginProvider, loginInfo.Login.ProviderKey, user.UserName, claim.ToString());
                    }
                }

                Trace.TraceInformation("Loading new user data for user {0}:{1}, local username {2}", loginInfo.Login.LoginProvider, loginInfo.Login.ProviderKey, user.UserName);
                await LoadNewUserDataAsync(user, loginInfo);

                return RedirectToLocal(returnUrl);
            }
        }

        private static readonly string BattleNetProviderId = @"BattleNet";
        private Task LoadNewUserDataAsync(GGCharityUser user, ExternalLoginInfo loginInfo)
        {
            if (loginInfo.Login.LoginProvider == BattleNetProviderId)
            {
                Trace.TraceInformation("Beginning to load BattleNet info for user {0}", user.UserName);
                var accessTokenClaim = loginInfo.ExternalIdentity.Claims.Where(c => c.Type == @"urn:battlenet:accesstoken").Single();
                return LoadBattleNetUserDataAsync(user, accessTokenClaim.Value);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private async Task LoadBattleNetUserDataAsync(GGCharityUser user, string accessToken)
        {
            Trace.TraceInformation("Calling GetProfileAsync for battle.net user {0}", user.UserName);
            var profile = await BattleNetApi.StarCraft.Api.GetProfileAsync(BattleNetApi.Region.US, accessToken);

            if (profile.Characters.Count > 1)
            {
                Trace.TraceEvent(TraceEventType.Critical, 0, "User {0} has {1} StarCraft characters!", user.Email, profile.Characters.Count);
                throw new NotSupportedException();
            }

            if (profile.Characters.Count == 0)
            {
                Trace.TraceInformation("User {0} has no StarCraft characters", user.Email);
                return;
            }

            await Data.PerformAsync(async () =>
            {
                GameProfile oldStarcraftProfile = user.GameProfiles.Where(p => p.GameId == GameID.StarCraft).SingleOrDefault();
                if (oldStarcraftProfile == null)
                {
                    Trace.TraceInformation("User {0} does not have a StarCraft game profile, creating one", user.UserName);

                    var gameProfile = new GameProfile
                    {
                        GameId = GameID.StarCraft,
                        ProfileData = profile.JsonString,
                        ProfileDataVersion = profile.Version,
                        UserId = user.Id,
                        AdditionalData = profile.Region.ToString()
                    };

                    user.GameProfiles.Add(gameProfile);
                    Data.GameProfiles.Add(gameProfile);
                }
                else
                {
                    Trace.TraceInformation("User {0} already has a StarCraft game profile, refreshing it", user.UserName);
                    oldStarcraftProfile.ResetData(profile.JsonString, profile.Version, profile.Region.ToString());
                }

                await Data.SaveChangesAsync();
                Trace.TraceInformation("User {0}'s StarCraft game profile was saved successfully", user.UserName);
            });
        }

        public async Task<ActionResult> RefreshGames(string returnUrl)
        {
            await Data.PerformAsync(async () =>
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                foreach (var login in user.Logins)
                {
                    await RefreshGamesForLoginAsync(user, login);
                }
            });
            return RedirectToLocal(returnUrl);
        }

        private async Task RefreshGamesForLoginAsync(GGCharityUser user, IdentityUserLogin login)
        {
            if (login.LoginProvider == "BattleNet")
            {
                IdentityUserClaim battleNetAccessTokenClaim = user.Claims.Where(c => c.ClaimType == @"urn:battlenet:accesstoken").SingleOrDefault();
                if (battleNetAccessTokenClaim == null)
                {
                    Trace.TraceEvent(TraceEventType.Critical, 0, "User {0} has a BattleNet login but no access token claim", user.Id);
                    return;
                }

                string battleNetAccessToken = battleNetAccessTokenClaim.ClaimValue;

                await LoadBattleNetUserDataAsync(user, battleNetAccessToken);
            }
        }

        //
        // POST: /Account/LinkLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LinkLogin(string provider, string returnUrl)
        {
            // Request a redirect to the external login provider to link a login for the current user
            return new ChallengeResult(provider, Url.Action("LinkLoginCallback", "Account", new { returnUrl = returnUrl }), User.Identity.GetUserId());
        }

        //
        // GET: /Account/LinkLoginCallback
        public async Task<ActionResult> LinkLoginCallback(string returnUrl)
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync(XsrfKey, User.Identity.GetUserId());
            if (loginInfo == null)
            {
                return RedirectToManageWithMessage("An unspecified error has occurred");
            }
            IdentityResult result = null;
            await Data.PerformAsync(async () =>
            {
                result = await UserManager.AddLoginAsync(User.Identity.GetUserId(), loginInfo.Login);
            });
            if (result.Succeeded)
            {
                if (returnUrl == null)
                {
                    return RedirectToAction("Manage");
                }
                else
                {
                    return Redirect(returnUrl);
                }
            }
            else
            {
                return RedirectToManageWithMessage(result.Errors.FirstOrDefault());
            }
        }

        private ActionResult RedirectToManageWithMessage(ManageMessageId message)
        {
            TempData["ManageMessage"] = message;
            return RedirectToAction("Manage");
        }

        private ActionResult RedirectToManageWithMessage(string message)
        {
            TempData["ManageMessage"] = ManageMessageId.Error;
            TempData["ManageMessageString"] = message;

            return RedirectToAction("Manage");
        }

        public ActionResult AccountSettings()
        {
            return View();
        }

        public async Task<ActionResult> ChangeTimeZone()
        {
            string timeZoneId = null;
            await Data.PerformAsync(async () =>
            {
                GGCharityUser user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                timeZoneId = user.TimeZoneId;
            });

            return View(timeZoneId);
        }

        //
        // POST: /Account/ExternalLoginConfirmation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeTimeZone(string timeZoneId)
        {
            if (ModelState.IsValid)
            {
                GGCharitySession.Get().TimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                await Data.PerformAsync(async () =>
                {
                    GGCharityUser user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                    user.TimeZoneId = timeZoneId;
                    await Data.SaveChangesAsync();
                });

                return RedirectToAction("AccountSettings");
            }
            return View(timeZoneId);
        }

        //
        // POST: /Account/ExternalLoginConfirmation
        [HttpPost]
        [AllowAnonymous]    
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string ReturnUrl)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Manage");
            }

            if (ModelState.IsValid)
            {
                // Get the information about the user from the external login provider
                var info = await AuthenticationManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    return View("ExternalLoginFailure");
                }
                var user = new GGCharityUser() { UserName = model.UserName, Email = model.Email };
                user.TimeZoneId = model.TimeZoneId;

                IdentityResult result = null;

                await Data.PerformAsync(async () =>
                {
                    result = await UserManager.CreateAsync(user);
                });

                if (result.Succeeded)
                {
                    await Data.PerformAsync(async () =>
                    {
                        result = await UserManager.AddLoginAsync(user.Id, info.Login);
                    });
                }

                if (result.Succeeded)
                {
                    await Data.PerformAsync(async () =>
                    {
                        await AddClaimsForNewUser(info, user);
                    });

                    await LoadNewUserDataAsync(user, info);
                }

                if (result.Succeeded)
                {
                    return await PostRegistrationSignIn(ReturnUrl, user);
                }
                AddErrors(result);
            }

            ViewBag.ReturnUrl = ReturnUrl;
            return View(model);
        }

        private async Task AddClaimsForNewUser(ExternalLoginInfo info, GGCharityUser user)
        {
            foreach (var claim in info.ExternalIdentity.Claims)
            {
                if (!claim.Type.Equals(ClaimTypes.NameIdentifier))
                {
                    await Data.PerformAsync(async () =>
                    {
                        await UserManager.AddClaimAsync(user.Id, claim);
                    });
                }
            }
        }

        private async Task<ActionResult> PostRegistrationSignIn(string ReturnUrl, GGCharityUser user)
        {
            await SignInAsync(user, isPersistent: false);

            // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771
            // Send an email with this link
            string code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
            var callbackUrl = UriExtensions.GetEmulationAwareUrl(Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme));

            // If auto-confirm is turned on, just send them straight to the confirmation URL
            if (Config.Get().AutoConfirmAccounts)
            {
                await Data.PerformAsync(async () =>
                {
                    await UserManager.ConfirmEmailAsync(user.Id, code);
                });
            }
            else
            {
                await UserManager.SendEmailAsync(user.Id, "Confirm your account", "Hi " + user.UserName + "!<br /><br />Thanks for signing up for GGCharity.  Before you can join an event or make a pledge, you'll have to confirm your email address.  Please <a href=\"" + callbackUrl + "\">click here</a> to proceed!<br /><br />-The GGCharity Team");
            }

            if (ReturnUrl == null)
            {
                ReturnUrl = Url.Action("Index", "Home");
            }
            return RedirectToLocal(ReturnUrl);
        }

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            LogOffCommon(true);
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Performs common logoff 
        /// </summary>
        /// <param name="abandonSession">Indicates whether the session should be abandoned.  This is set to false in the SwitchAccount scenario
        /// because if the session is abandoned, the subsequent logon is also thrown away.</param>
        private void LogOffCommon(bool abandonSession)
        {
            if (abandonSession)
            { 
                GGCharitySession.Get().Abandon();
            }
            AuthenticationManager.SignOut();
        }

        //
        // GET: /Account/ExternalLoginFailure
        [AllowAnonymous]
        public ActionResult ExternalLoginFailure()
        {
            return View();
        }

        [ChildActionOnly]
        public ActionResult RemoveAccountList()
        {
            return Data.Perform(() =>
            {
                var linkedAccounts = UserManager.GetLogins(User.Identity.GetUserId());
                ViewBag.ShowRemoveButton = HasPassword() || linkedAccounts.Count > 1;
                return (ActionResult)PartialView("_RemoveAccountPartial", linkedAccounts);
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && UserManager != null)
            {
                UserManager.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private async Task SignInAsync(GGCharityUser user, bool isPersistent)
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ExternalCookie);
            AuthenticationManager.SignIn(new AuthenticationProperties() { IsPersistent = isPersistent }, await user.GenerateUserIdentityAsync(UserManager));
            GGCharitySession.Get().Initialize(user);
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private bool HasPassword()
        {
            return Data.Perform(() =>
            {
                var user = UserManager.FindById(User.Identity.GetUserId());
                if (user != null)
                {
                    return user.PasswordHash != null;
                }
                return false;
            });
        }


        public enum ManageMessageId
        {
            ChangePasswordSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            ExternalLoginAlreadyExists,
            Error,
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        private class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri) : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties() { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }
        #endregion
    }
}