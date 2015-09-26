using GGCharityWebRole.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity.Owin;

namespace GGCharityWebRole.Controllers
{
    [RequireHttps]
    public class UserController : BaseController
    {
        public UserController()
            : base("UserController")
        {

        }

        // GET: User/Id
        public async Task<ActionResult> Index(string Id)
        {
            return await Data.PerformAsync<ActionResult>(async () =>
            {
                if (String.IsNullOrWhiteSpace(Id))
                {
                    if (User.Identity.IsAuthenticated)
                    {
                        Id = User.Identity.Name;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }

                var user = await UserManager.FindByNameAsync(Id);
                if (user == null)
                {
                    return HttpNotFound();
                }

                if (User.Identity.IsAuthenticated
                    && Id.Equals(User.Identity.Name))
                {
                    var privateModel = await Data.GetModelAsync<UserPrivateViewModel>(() => { return new UserPrivateViewModel(User.Identity.Name); });
                    return View("UserPrivateView", privateModel);
                }
                else
                {
                    var model = await Data.GetModelAsync<UserPublicViewModel>(() => { return new UserPublicViewModel(Id, GGCharitySession.Get().AnonymousId); });
                    return View("UserPublicView", model);
                }
            });
        }
    }
}