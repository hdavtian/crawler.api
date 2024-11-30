using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Models;

namespace CrawlerWebApi.Interfaces
{
    public interface ITestService
    {
        Task<List<CrawlTest>> GetCrawlTestsAsync();
        Task<CrawlTest> GetCrawlTestAsync(string Guid);
        Task<List<PageScreenshot>> GetPageScreenshotsAsync(string Guid);
        Task<List<AppScreenshot>> GetAppScreenshotsAsync(string Guid);
        Task<List<UrlModel>> GetCrawledUrlsAsync(string Guid);
        Task<List<IcPage>> GetPageAndAppSummaryAsync(string Guid);
        Task<List<AppArtifactManifest>> GetAppArtifactsAsync(string Guid);

    }
}
