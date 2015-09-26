using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CloudCore.Telemetry;
using GGCharityData;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace GGCharityWebRole.Core
{
    public class GGCharityTelemetry : TelemetryManager
    {
        private const string TelemetryAlertSubject = "[GGC TELEMETRY ALERT] {0} alert(s) at {1}-UTC";
        private static GGCharityTelemetry Telemetry;
        private static System.Threading.Timer TelemetryTimer;

        public static void ConfigureTelemetry()
        {
            Telemetry = new GGCharityTelemetry();
            Telemetry.OnTelemetryAlert += Telemetry_OnTelemetryAlert;
            TelemetryManager.SetApplicationTelemetryManager(() => { return Telemetry; });
            var reportingFrequency = Telemetry.ReportingFrequency();
            TelemetryTimer = new System.Threading.Timer(TelemetryTimerCallback, Telemetry, (int)(reportingFrequency.TotalMilliseconds / 2), System.Threading.Timeout.Infinite);
        }

        private static void TelemetryTimerCallback(object state)
        {
            var reportingFrequency = Telemetry.ReportingFrequency();
            GGCharityTelemetry telemetry = (GGCharityTelemetry)state;
            var task = telemetry.CommitAndAlertAsync();
            task.ContinueWith((t) =>
            {
                TelemetryTimer = new System.Threading.Timer(TelemetryTimerCallback, Telemetry, (int)(reportingFrequency.TotalMilliseconds / 2), System.Threading.Timeout.Infinite);
            });
        }

        static void Telemetry_OnTelemetryAlert(List<TelemetryAlert> alerts)
        {
            string subject = String.Format(TelemetryAlertSubject, alerts.Count, DateTime.UtcNow);
            StringBuilder messageBuilder = new StringBuilder();
            messageBuilder.AppendLine(subject);
            messageBuilder.AppendLine();

            foreach (var alert in alerts)
            {
                messageBuilder.AppendFormat("Telemetry point {0} success rate fell below {1}% to {2}%", alert.Id, alert.ReportThreshold * 100, alert.SuccessRate * 100);
                messageBuilder.AppendLine();
            }

            var mailer = GGCharityCore.Email.Get();
            mailer.SendAsync(Config.Get().TelemetryAlertAddress, subject, messageBuilder.ToString());
        }

        private static ITelemetryDatabase TelemetryDbConstructor()
        {
            return GGCharityWebRole.DataContext.GetCacheless();
        }

        public GGCharityTelemetry() :
            base(
                () => { return TimeSpan.FromSeconds(Config.Get().TelemetryReportFrequencySeconds); }, 
                () => { return TimeSpan.FromSeconds(Config.Get().TelemetryReportSpanSeconds); }, 
                Log.DefaultTrace, 
                TelemetryDbConstructor)
        {

        }
    }
}