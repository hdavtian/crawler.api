using NLog;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CrawlerWebApi.Services
{
    public class WriterQueueService
    {
        private readonly Channel<Func<Task>> _channel;
        private readonly Logger _logger;

        public WriterQueueService(int capacity = 100)
        {
            _logger = _logger = NLog.LogManager.GetCurrentClassLogger();

            // Bounded channel with a capacity to prevent overwhelming the system
            _channel = Channel.CreateBounded<Func<Task>>(capacity);

            // Start the background task to consume from the channel
            _ = Task.Run(ProcessQueueAsync);
        }

        // Enqueue a new task
        public async Task EnqueueAsync(Func<Task> taskGenerator)
        {
            await _channel.Writer.WriteAsync(taskGenerator);
        }

        // Background processing of queued tasks
        private async Task ProcessQueueAsync()
        {
            // Continuously read and execute tasks from the channel
            while (await _channel.Reader.WaitToReadAsync())
            {
                while (_channel.Reader.TryRead(out var task))
                {
                    try
                    {
                        // Execute the task
                        await task();
                    }
                    catch (Exception ex)
                    {
                        // Log the error using NLog
                        _logger.Error(ex, "Error processing task in the queue");
                    }
                }
            }
        }
    }
}
