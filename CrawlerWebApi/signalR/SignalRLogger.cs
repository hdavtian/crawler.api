using Microsoft.AspNetCore.SignalR;
using NLog.Targets;
using NLog;

namespace CrawlerWebApi.signalR
{
    [Target("SignalR")]
    public class SignalRLogger
    {
        private static IHubContext<LoggingHub> HubContext;

        // buffering early logs until front end joins signalr group
        private static readonly Dictionary<string, List<string>> EarlyLogBuffer = new();
        private static readonly object BufferLock = new(); // Lock for thread safety

        // Static method to set the IHubContext via DI
        public static void SetHubContext(IHubContext<LoggingHub> hubContext)
        {
            HubContext = hubContext;
        }

        public static void SendLogMessage(string testId, string message)
        {
            if (!string.IsNullOrEmpty(testId) && HubContext != null)
            {
                // Try sending the log message directly
                try
                {
                    HubContext.Clients.Group(testId).SendAsync("ReceiveLogMessage", new { testId, message });
                }
                catch
                {
                    // If the group isn't ready, buffer the log
                    lock (BufferLock)
                    {
                        if (!EarlyLogBuffer.ContainsKey(testId))
                        {
                            EarlyLogBuffer[testId] = new List<string>();
                        }
                        EarlyLogBuffer[testId].Add(message);
                    }
                }
            }
            else
            {
                // Always buffer if HubContext or testId is invalid
                lock (BufferLock)
                {
                    if (!EarlyLogBuffer.ContainsKey(testId))
                    {
                        EarlyLogBuffer[testId] = new List<string>();
                    }
                    EarlyLogBuffer[testId].Add(message);
                }
            }
        }

        public static async Task FlushBufferedLogs(string testId)
        {
            if (!string.IsNullOrEmpty(testId) && EarlyLogBuffer.ContainsKey(testId))
            {
                List<string> bufferedLogs;
                lock (BufferLock)
                {
                    // Retrieve and remove buffered logs for this testId
                    bufferedLogs = EarlyLogBuffer[testId];
                    EarlyLogBuffer.Remove(testId);
                }

                foreach (var log in bufferedLogs)
                {
                    await HubContext.Clients.Group(testId).SendAsync("ReceiveLogMessage", new { testId, message = log });
                }
            }
        }
    }
}
