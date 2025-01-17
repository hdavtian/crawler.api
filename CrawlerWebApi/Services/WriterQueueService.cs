using IC.Test.Playwright.Crawler.Providers.Logger;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CrawlerWebApi.Services
{
    public class WriterQueueService
    {
        private readonly Channel<Func<Task>> Channel;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private ILoggingProvider Logger;

        public WriterQueueService(IServiceScopeFactory serviceScopeFactory, int capacity = 100)
        {
            _serviceScopeFactory = serviceScopeFactory;

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                Logger = scope.ServiceProvider.GetRequiredService<ILoggingProvider>();

                // Bounded channel with a capacity to prevent overwhelming the system
                Channel = System.Threading.Channels.Channel.CreateBounded<Func<Task>>(capacity);

                // Start the background task to consume from the channel
                _ = Task.Run(ProcessQueueAsync);
            }
        }

        // Enqueue a new task
        public async Task EnqueueAsync(Func<Task> taskGenerator)
        {
            await Channel.Writer.WriteAsync(taskGenerator);
        }

        // Background processing of queued tasks
        private async Task ProcessQueueAsync()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                Logger = scope.ServiceProvider.GetRequiredService<ILoggingProvider>();

                // Continuously read and execute tasks from the channel
                while (await Channel.Reader.WaitToReadAsync())
                {
                    while (Channel.Reader.TryRead(out var task))
                    {
                        try
                        {
                            // Execute the task
                            await task();
                        }
                        catch (Exception ex)
                        {
                            // Log the error using NLog
                            Logger.Error(ex, "Error processing task in the queue");
                        }
                    }
                }
            }
        }
    }
}
