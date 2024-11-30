using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace CrawlerWebApi.signalR
{
    public class LoggingHub : Hub
    {
        public async Task SendLogMessage(string message)
        {
            try
            {
                await Clients.All.SendAsync("ReceiveLogMessage", message);
            }
            catch (Exception ex)
            {
                // Log exception (e.g., using NLog)
                Console.WriteLine($"Error sending log message: {ex.Message}");
            }
        }
    }
}
