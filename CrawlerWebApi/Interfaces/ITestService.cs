using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Models;

namespace CrawlerWebApi.Interfaces
{
    public interface ITestService
    {
        Task<TestModel> GetCrawlTestAsync(string Guid);
        Task<List<PageScreenshot>> GetPageScreenshotsAsync(string Guid);
        Task<List<AppScreenshot>> GetAppScreenshotsAsync(string Guid);
        Task<List<UrlModel>> GetCrawledUrlsAsync(string Guid);
        
    }
}
