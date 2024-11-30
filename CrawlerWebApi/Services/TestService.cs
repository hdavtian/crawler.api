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

        public TestService(
            IConfiguration AppConfiguration,
            CrawlArtifacts CrawlerArtifacts)
        {
            this.AppConfiguration = AppConfiguration;
            SiteArtifactsWinPath = this.AppConfiguration.GetValue<string>("SiteArtifactsWinPath");
            this.CrawlerArtifacts = CrawlerArtifacts;
        }

        public async Task<CrawlTest> GetCrawlTestAsync(string guid)
        {
            return await CrawlerArtifacts.GetCrawlTestAsync(guid);
        }
        public async Task<List<CrawlTest>> GetCrawlTestsAsync()
        {
            return await CrawlerArtifacts.GetCrawlTestsAsync();
        }

        public async Task<List<PageScreenshot>> GetPageScreenshotsAsync(string guid)
        {
            return await CrawlerArtifacts.GetPageScreenshots(Guid.Parse(guid));
        }

        public async Task<List<AppScreenshot>> GetAppScreenshotsAsync(string guid)
        {
            return await CrawlerArtifacts.GetAppScreenshots(Guid.Parse(guid));
        }

        public async Task<List<UrlModel>> GetCrawledUrlsAsync(string guid)
        {
            return await CrawlerArtifacts.GetCrawledUrls(Guid.Parse(guid));
        }

        public async Task<List<IcPage>> GetPageAndAppSummaryAsync(string guid)
        {
            return await CrawlerArtifacts.GetPageAndAppSummary(Guid.Parse(guid));
        }

        public async Task<List<AppArtifactManifest>> GetAppArtifactsAsync(string guid)
        {
            return await CrawlerArtifacts.GetAppArtifacts(Guid.Parse(guid));
        }
    }
}
