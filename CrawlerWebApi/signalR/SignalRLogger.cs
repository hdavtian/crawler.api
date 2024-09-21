using Microsoft.AspNetCore.SignalR;
using NLog.Targets;
using NLog;

namespace CrawlerWebApi.signalR
{
    [Target("SignalR")]
    public class SignalRLogger
    {
        private static IHubContext<LoggingHub> _hubContext;

        // Static method to set the IHubContext via DI
        public static void SetHubContext(IHubContext<LoggingHub> hubContext)
        {
            _hubContext = hubContext;
        }

        // Static method called by NLog
        public static void SendLogMessage(string message)
        {
            if (_hubContext != null)
            {
                _hubContext.Clients.All.SendAsync("ReceiveLogMessage", message);
            }
        }
    }
}
