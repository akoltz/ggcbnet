using GGCharityCore;
using GGCharityWebRole.Core;
using LogCore;
using Microsoft.Owin;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Owin;
using System;
using TestServices;

[assembly: OwinStartupAttribute(typeof(GGCharityWebRole.Startup))]
namespace GGCharityWebRole
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            GGCharityInstance.InitializeLogging();
#if TEST
            InitTestServices();
#endif
            ConfigureAuth(app);
        }

#if TEST
        static TestEnvironment _testEnvironment;
        private void InitTestServices()
        {
            _testEnvironment = new TestEnvironment(RoleEnvironment.CurrentRoleInstance.Id);
            _testEnvironment.Start();
        }
#endif
    }
}
