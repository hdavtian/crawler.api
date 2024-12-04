using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Models;

namespace CrawlerWebApi.Interfaces
{
    public interface ITestService
    {
        Task<List<CrawlTest>> GetCrawlTests();
        Task<CrawlTest> GetCrawlTest(string Guid);
        Task<List<PageScreenshot>> GetPageScreenshots(string Guid);
        Task<List<AppScreenshot>> GetAppScreenshots(string Guid);
        Task<List<UrlModel>> GetCrawledUrls(string Guid);
        Task<List<IcPage>> GetPageAndAppSummary(string Guid);
        Task<List<AppArtifactManifest>> GetAppArtifacts(string Guid);
        Task<string> GetAppHtml(string TestGuid, string AppGuid);
        Task<string> GetAppText(string TestGuid, string AppGuid);
        Task<List<DiffTest>> GetDiffTests();
        Task<DiffTest> GetDiffTest(string Guid);

    }
}
