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
        private readonly CrawlArtifacts CrawlerArtifacts;
        private readonly DiffArtifacts DiffArtifacts;

        public TestService(
            IConfiguration AppConfiguration,
            CrawlArtifacts CrawlerArtifacts,
            DiffArtifacts DiffArtifacts)
        {
            this.AppConfiguration = AppConfiguration;
            SiteArtifactsWinPath = this.AppConfiguration.GetValue<string>("SiteArtifactsWinPath");
            this.CrawlerArtifacts = CrawlerArtifacts;
            this.DiffArtifacts = DiffArtifacts;
        }

        public async Task<CrawlTest> GetCrawlTest(string guid)
        {
            return await CrawlerArtifacts.GetCrawlTest(guid);
        }
        public async Task<List<CrawlTest>> GetCrawlTests()
        {
            return await CrawlerArtifacts.GetCrawlTests();
        }

        public async Task<List<PageScreenshot>> GetPageScreenshots(string guid)
        {
            return await CrawlerArtifacts.GetPageScreenshots(Guid.Parse(guid));
        }

        public async Task<List<AppScreenshot>> GetAppScreenshots(string guid)
        {
            return await CrawlerArtifacts.GetAppScreenshots(Guid.Parse(guid));
        }

        public async Task<List<UrlModel>> GetCrawledUrls(string guid)
        {
            return await CrawlerArtifacts.GetCrawledUrls(Guid.Parse(guid));
        }

        public async Task<List<IcPage>> GetPageAndAppSummary(string guid)
        {
            return await CrawlerArtifacts.GetPageAndAppSummary(Guid.Parse(guid));
        }

        public async Task<List<AppArtifactManifest>> GetAppArtifacts(string guid)
        {
            return await CrawlerArtifacts.GetAppArtifacts(Guid.Parse(guid));
        }

        public async Task<string> GetAppHtml(string testGuid, string appGuid)
        {
            return await CrawlerArtifacts.GetAppHtmlContent(Guid.Parse(testGuid), Guid.Parse(appGuid));
        }

        public async Task<string> GetAppText(string testGuid, string appGuid)
        {
            return await CrawlerArtifacts.GetAppTextContent(Guid.Parse(testGuid), Guid.Parse(appGuid));
        }
        /* Diff tests*/
        public async Task<List<DiffTest>> GetDiffTests()
        {
            return await DiffArtifacts.GetDiffTests();
        }
        public async Task<DiffTest> GetDiffTest(string guid)
        {
            return await DiffArtifacts.GetDiffTest(guid);
        }

        public async Task<List<AppDiffs>> GetAllAppDiffs(string TestGuid)
        {
            return await DiffArtifacts.GetAllAppDiffs(Guid.Parse(TestGuid));
        }
    }
}
