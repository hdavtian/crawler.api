using CrawlerWebApi.Models;

namespace CrawlerWebApi.Interfaces
{
    public interface IDiffTestService
    {
        Task<TestResult> RunDiffTestAsync(DiffTestPostRequestModel request);
    }
}
