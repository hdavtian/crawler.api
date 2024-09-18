using CrawlerWebApi.Models;

namespace CrawlerWebApi.Interfaces
{
    public interface ITestService
    {
        Task<TestResult> RunBaselineTestAsync(BaselineTestPostRequestModel request);
    }
}
