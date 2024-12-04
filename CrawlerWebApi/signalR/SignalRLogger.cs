using Microsoft.AspNetCore.SignalR;
using NLog.Targets;
using NLog;

namespace CrawlerWebApi.signalR
{
    [Target("SignalR")]
    public class SignalRLogger
    {
        private static IHubContext<LoggingHub> HubContext;

        // Static method to set the IHubContext via DI
        public static void SetHubContext(IHubContext<LoggingHub> hubContext)
        {
            hubContext = hubContext;
        }

        // Static method called by NLog
        public static void SendLogMessage(string message)
        {
            if (HubContext != null)
            {
                HubContext.Clients.All.SendAsync("ReceiveLogMessage", message);
            }
        }
    }
}
