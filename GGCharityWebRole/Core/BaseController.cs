using GGCharityCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.WindowsAzure.ServiceRuntime;
using GGCharityData;
using System.Threading.Tasks;
using LogCore;
using Microsoft.Owin;
using Microsoft.AspNet.Identity.Owin;

namespace GGCharityWebRole
{
    public class BaseController : Controller
    {
        string _controllerName;
        protected TraceSource Trace;
#if TEST
        private static bool isDeploymentComplete;
#endif
        private GGCharityWebDatabase _Data;
        protected GGCharityWebDatabase Data
        {
            get
            {
                if (_Data == null)
                {
                    lock (this)
                    {
                        if (_Data == null)
                        {
                            GGCharityDatabase.GetRetryPolicy().ExecuteAction(() =>
                            {
                                _Data = GGCharityWebDatabase.Get(this);
                            });
                        }
                    }
                }
                return _Data;
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
        }

        public BaseController(string controllerName)
        {
            _controllerName = controllerName;
            Trace = Logging.GetLog(controllerName, controllerName);
        }

        protected string GetAnonymousId()
        {
            if (Request == null)
            {
                return "NoRequest";
            }

            if (Request.GetOwinContext() == null)
            {
                return "NoGetOwinContext";
            }

            if (Request.GetOwinContext().Authentication == null)
            {
                return "NoAuthentication";
            }

            if (Request.GetOwinContext().Authentication.User == null)
            {
                return "NoUser";
            }

            if (Request.GetOwinContext().Authentication.User.Identity == null)
            {
                return "NoIdentity";
            }

            if (GGCharitySession.Get() == null)
            {
                return "NoSession";
            }

            return (Request.GetOwinContext().Authentication.User.Identity.IsAuthenticated) ? GGCharitySession.Get().AnonymousId : "anonymous";
        }

        protected string GetUserName()
        {
            return (Request.GetOwinContext().Authentication.User.Identity.Name);
        }

        public UserOperation UserOperation
        {
            get;
            private set;
        }

        public async Task<GGCharityUser> GetCurrentUserAsync()
        {
            return await Data.GetUserManager().FindByIdAsync(GGCharitySession.Get().AnonymousId);
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

#if TEST
            if (!filterContext.ActionDescriptor.ControllerDescriptor.ControllerType.Equals(typeof(GGCharityWebRole.Controllers.TestServicesController)))
            {
                if (!isDeploymentComplete)
                {
                    isDeploymentComplete = !GGCharityInstance.IsDeploymentPendingAsync().Result;
                    if (!isDeploymentComplete)
                    {
                        // In azure emulation, ports 8080 are used by the azure service and it forwards to 8081
                        // on which IIS actually listens.  The URL is to 8080, but IIS will see a request to 8081.
                        // If we try to return to the requested URL at 8081, we'll fail.
                        Uri returnUrl = filterContext.RequestContext.HttpContext.Request.GetEmulationAwareUrl();
                        filterContext.Result = RedirectToAction("WaitForDeployment", "TestServices", new { returnUrl = returnUrl });
                    }
                }
            }
#endif

            if (TempData.ContainsKey("DevClearCache")
                && (bool)TempData["DevClearCache"])
            {
                GGCharityWebRole.Core.GGCharityOutputCache.Get().Remove(filterContext.ActionDescriptor.ControllerDescriptor.ControllerName, filterContext.ActionDescriptor.ActionName);
            }

            UserOperation = UserOperation.Initialize(Trace, _controllerName, filterContext.ActionDescriptor.ActionName, GetAnonymousId());

            UserOperation.Details.TryAdd("AnonymousUserId", GetAnonymousId());
            UserOperation.Details.TryAdd("WebRoleInstance", RoleEnvironment.CurrentRoleInstance.Id);
            UserOperation.Details.TryAdd("BinaryVersion", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());

            ViewBag.UserOperation = UserOperation;
        }

        protected override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            if (!filterContext.ExceptionHandled && filterContext.Exception != null)
            {
                throw new Exception("An exception was thrown by the action.", filterContext.Exception);
            }

            if (Session != null)
            {
                UserOperation.Details.TryAdd("Session.EffectiveTime", GGCharitySession.Get().EffectiveTime);
                UserOperation.Details.TryAdd("Session.IsNewSession", Session.IsNewSession);
                UserOperation.Details.TryAdd("Session.SessionId", Session.SessionID);
            }
            else
            {
                UserOperation.Details.TryAdd("Session", "null");
            }

            base.OnActionExecuted(filterContext);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _Data != null)
            {
                _Data.Dispose();
            }

            if (disposing && UserOperation != null)
            { 
                UserOperation.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}