using CrawlerWebApi.Models;

namespace CrawlerWebApi.Interfaces
{
    public interface IBaselineTestService
    {
        Task<TestResult> RunBaselineTestAsync(BaselineTestPostRequestModel request);
    }
}
