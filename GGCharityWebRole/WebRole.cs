using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using AzureCore;
using System.Diagnostics;
using Microsoft.ApplicationServer.Caching;
using GGCharityWebRole;
using GGCharityWebRole.Core;
using LogCore;
using GGCharityCore;
using MatchHistoryStorage;

namespace Ggsc2WebRole
{
    public class WebRole : RoleEntryPoint
    {
        private GGCharityInstance Instance;
        private WebRoleBackgroundWorker BackgroundWorker;
        private LongLivedTraceSource Log;
        private System.Threading.Timer WebKeepAliveTimer;
        private volatile bool isReady = false;

        // For information on handling configuration changes
        // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
        public override bool OnStart()
        {
            GGCharityInstance.InitializeLogging();

            Log = Logging.GetLongLivedLog(RoleEnvironment.CurrentRoleInstance.Id, "WebRole");
            WebKeepAliveTimer = new System.Threading.Timer(WebKeepaliveTimer, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

            Log.TraceInformation("New web role initializing");

            RoleEnvironment.Changing += RoleEnvironment_Changing;
            RoleEnvironment.Changed += RoleEnvironment_Changed;
            RoleEnvironment.Stopping += RoleEnvironment_Stopping;
            RoleEnvironment.StatusCheck += RoleEnvironment_StatusCheck;

            bool result = base.OnStart();
            if (!result)
            {
                Log.TraceEvent(TraceEventType.Critical, 0, "base.OnStart failed");
            }

            //BackgroundWorker = new WebRoleBackgroundWorker(RoleEnvironment.CurrentRoleInstance.Id);
            //BackgroundWorker.Start();

            if (Config.Get().RunBackgroundWorkFromWebRole)
            {
                // In debug configurations, only start the background workers by request.
                // In test configurations, always start them.
#if !DEBUG || TEST
                Instance = new GGCharityInstance(RoleEnvironment.CurrentRoleInstance.Id, GGCharityInstance.InstanceType.Web);
                Instance.Start();
#endif
            }

            isReady = result;
            return result;
        }

        void RoleEnvironment_StatusCheck(object sender, RoleInstanceStatusCheckEventArgs e)
        {
            if (!isReady)
            {
                e.SetBusy();
            }
        }

        void RoleEnvironment_Stopping(object sender, RoleEnvironmentStoppingEventArgs e)
        {
            if (!Config.Get().InDevelopmentEnvironment)
            {
                Log.TraceEvent(TraceEventType.Critical, 0, "Web role {0} is stopping", RoleEnvironment.CurrentRoleInstance.Id);
            }
        }

        public override void OnStop()
        {
            base.OnStop();
        }

        void RoleEnvironment_Changed(object sender, RoleEnvironmentChangedEventArgs e)
        {
            Log.TraceInformation("Recevied a configuration change notification");
            Config.Get().OnConfigurationChanged(e.Changes);
        }

        private void RoleEnvironment_Changing(object sender, RoleEnvironmentChangingEventArgs e)
        {
            // Setting cancel to false indicates that this instance should _not_ be restarted
            // in response to the configuration change.
            e.Cancel = Config.Get().OnConfigurationChanging(e.Changes);
            if (e.Cancel)
            {
                Log.TraceInformation("Role will be restarted due to configuration change");
            }
        }

        private static void WebKeepaliveTimer(object state)
        {
            try
            {
                System.Net.WebClient wc = new System.Net.WebClient();
                wc.DownloadString(Config.Get().PokeUrl);
            }
            catch(Exception)
            {
            }
        }
    }
}
