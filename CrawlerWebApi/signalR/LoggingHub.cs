using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace CrawlerWebApi.signalR
{
    public class LoggingHub : Hub
    {
        public async Task SendLogMessage(string message)
        {
            await Clients.All.SendAsync("ReceiveLogMessage", message);
        }
    }
}
