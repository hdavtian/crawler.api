using System.Collections.Concurrent;
using CrawlerWebApi.Models;
using Microsoft.AspNetCore.SignalR;
using IC.Test.Playwright.Crawler.SignalR;

namespace CrawlerWebApi.Services
{
    public class TestRegistryService
    {
        private readonly ConcurrentDictionary<Guid, TestStatus> _runningTests = new();
        private readonly IHubContext<LoggingHub> _hub;

        public TestRegistryService(IHubContext<LoggingHub> hub)
        {
            _hub = hub;
        }

        public async Task RegisterTest(Guid testId, TestStatus status)
        {
            _runningTests[testId] = status;

            await _hub.Clients.Group("RunningTestsGlobal").SendAsync("TestStarted", status);
        }

        public async Task MarkTestCompleted(Guid testId)
        {
            if (_runningTests.TryRemove(testId, out var status))
            {
                await _hub.Clients.Group("RunningTestsGlobal").SendAsync("TestEnded", status);
            }
        }

        public IReadOnlyCollection<TestStatus> GetAllRunningTests() =>
            _runningTests.Values.ToList();
    }
}
