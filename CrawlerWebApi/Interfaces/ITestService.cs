using CrawlerWebApi.Models;
using CrawlerWebApi.Services;
using IC.Test.Playwright.Crawler.Models;

namespace CrawlerWebApi.Interfaces
{
    public interface ITestService
    {
        Task<List<CrawlTest>> GetCrawlTests();
        Task<CrawlTest> GetCrawlTest(string testGuid);
        Task<List<PageScreenshot>> GetPageScreenshots(string testGuid);
        Task<List<AppScreenshot>> GetAppScreenshots(string testGuid);
        Task<List<UrlModel>> GetCrawledUrls(string testGuid);
        Task<List<IcPage>> GetPageAndAppSummary(string testGuid);
        Task<List<AppArtifactManifest>> GetAppArtifacts(string testGuid);
        Task<string> GetAppHtml(string testGuid, string appGuid);
        Task<string> GetAppText(string testGuid, string appGuid);
        Task<List<DiffTest>> GetDiffTests();
        Task<DiffTest> GetDiffTest(string testGuid);
        Task<List<AppDiffs>> GetAllAppDiffs(string testGuid);
        Task<AppDiffs> GetAppDiffs(string testGuid, string appGuid);
        Task<List<ScreenshotDiff>> GetAllPageScreenshotDiffs(string testGuid);
        Task<ScreenshotDiff> GetPageScreenshotDiff(string testGuid, string pageGuid);
        Task<List<DiffTestMissingArtifact>> GetMissingArtifacts(string testGuid);
        Task<List<JsConsoleError>> GetJsConsoleErrors(string testGuid);
        Task<List<PageXhrTimingsGroupWithUrlModel>> GetPageXHRTimes(string testGuid);
    }
}
