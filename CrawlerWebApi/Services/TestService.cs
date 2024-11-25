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
        private readonly IConfiguration _configuration;
        private readonly string _siteArtifactsWinPath;
        private readonly CrawlArtifacts _crawlerArtifacts;

        public TestService(
            IConfiguration configuration,
            CrawlArtifacts crawlerArtifacts)
        {
            _configuration = configuration;
            _siteArtifactsWinPath = _configuration.GetValue<string>("SiteArtifactsWinPath");
            _crawlerArtifacts = crawlerArtifacts;
        }

        public async Task<TestModel> GetCrawlTestAsync(string guid)
        {
            return await _crawlerArtifacts.GetCrawlTestAsync(guid);
        }

        public async Task<List<PageScreenshot>> GetPageScreenshotsAsync(string guid)
        {
            return await _crawlerArtifacts.GetPageScreenshots(Guid.Parse(guid));
        }

        public async Task<List<AppScreenshot>> GetAppScreenshotsAsync(string guid)
        {
            return await _crawlerArtifacts.GetAppScreenshots(Guid.Parse(guid));
        }

        public async Task<List<UrlModel>> GetCrawledUrlsAsync(string guid)
        {
            return await _crawlerArtifacts.GetCrawledUrls(Guid.Parse(guid));
        }
    }
}
