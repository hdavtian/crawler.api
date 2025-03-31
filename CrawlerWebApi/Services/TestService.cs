using CrawlerWebApi.Interfaces;
using IC.Test.Playwright.Crawler.Models;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System;
using IC.Test.Playwright.Crawler.Utility;

namespace CrawlerWebApi.Services
{
    public class TestService : ITestService
    {
        private readonly IConfiguration AppConfiguration;
        private readonly string SiteArtifactsWinPath;
        private readonly CrawlArtifactManager CrawlArtifactManager;
        private readonly DiffArtifactManager DiffArtifactManager;

        public TestService(
            IConfiguration AppConfiguration,
            CrawlArtifactManager CrawlArtifactManager,
            DiffArtifactManager DiffArtifactManager)
        {
            this.AppConfiguration = AppConfiguration;
            SiteArtifactsWinPath = this.AppConfiguration.GetValue<string>("SiteArtifactsWinPath");
            this.CrawlArtifactManager = CrawlArtifactManager;
            this.DiffArtifactManager = DiffArtifactManager;
        }

        public async Task<CrawlTest> GetCrawlTest(string testGuid)
        {
            return await CrawlArtifactManager.GetCrawlTest(testGuid);
        }
        public async Task<List<CrawlTest>> GetCrawlTests()
        {
            return await CrawlArtifactManager.GetCrawlTests();
        }

        public async Task<List<PageScreenshot>> GetPageScreenshots(string testGuid)
        {
            return await CrawlArtifactManager.GetPageScreenshots(Guid.Parse(testGuid));
        }

        public async Task<List<AppScreenshot>> GetAppScreenshots(string testGuid)
        {
            return await CrawlArtifactManager.GetAppScreenshots(Guid.Parse(testGuid));
        }

        public async Task<List<UrlModel>> GetCrawledUrls(string testGuid)
        {
            return await CrawlArtifactManager.GetCrawledUrls(Guid.Parse(testGuid));
        }

        public async Task<List<IcPage>> GetPageAndAppSummary(string testGuid)
        {
            return await CrawlArtifactManager.GetPageAndAppSummary(Guid.Parse(testGuid));
        }

        public async Task<List<AppArtifactManifest>> GetAppArtifacts(string testGuid)
        {
            return await CrawlArtifactManager.GetAppArtifacts(Guid.Parse(testGuid));
        }

        public async Task<string> GetAppHtml(string testGuid, string appGuid)
        {
            return await CrawlArtifactManager.GetAppHtmlContent(Guid.Parse(testGuid), Guid.Parse(appGuid));
        }

        public async Task<string> GetAppText(string testGuid, string appGuid)
        {
            return await CrawlArtifactManager.GetAppTextContent(Guid.Parse(testGuid), Guid.Parse(appGuid));
        }
        public async Task<List<JsConsoleError>> GetJsConsoleErrors(string testGuid)
        {
            return await CrawlArtifactManager.GetJsConsoleErrors(Guid.Parse(testGuid));
        }
        public async Task<List<PageXhrTimingsGroupWithUrlModel>> GetPageXHRTimes(string testGuid)
        {
            return await CrawlArtifactManager.GetPageXHRTimes(Guid.Parse(testGuid));
        }
        /* Diff tests*/
        public async Task<List<DiffTest>> GetDiffTests()
        {
            return await DiffArtifactManager.GetDiffTests();
        }
        public async Task<DiffTest> GetDiffTest(string testGuid)
        {
            return await DiffArtifactManager.GetDiffTest(testGuid);
        }

        public async Task<List<AppDiffs>> GetAllAppDiffs(string testGuid)
        {
            return await DiffArtifactManager.GetAllAppDiffs(Guid.Parse(testGuid));
        }

        public async Task<AppDiffs> GetAppDiffs(string testGuid, string appGuid)
        {
            return await DiffArtifactManager.GetAppDiff(Guid.Parse(testGuid), Guid.Parse(appGuid));
        }

        public async Task<List<ScreenshotDiff>> GetAllPageScreenshotDiffs(string testGuid)
        {
            return await DiffArtifactManager.GetAllPageScreenshotDiffs(Guid.Parse(testGuid));
        }

        public async Task<ScreenshotDiff> GetPageScreenshotDiff(string testGuid, string appGuid)
        {
            return await DiffArtifactManager.GetPageScreenshotDiff(Guid.Parse(testGuid), Guid.Parse(appGuid));
        }

        public async Task<List<DiffTestMissingArtifact>> GetMissingArtifacts(string testGuid)
        {
            return await DiffArtifactManager.GetMissingArtifacts(Guid.Parse(testGuid));
        }
    }
}
