using IC.Test.Playwright.Crawler.Models;

namespace CrawlerWebApi.Models
{
    public class BaselineTestPostRequestModel
    {
        public string Url { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Headless { get; set; }
        public string Browser {  get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public bool RecordVideo { get; set; }
        public bool TakePageScreenshots {  get; set; }
        public bool TakeAppScreenshots { get; set; }
        public bool CaptureAppHtml { get; set; }
        public bool CaptureAppText { get; set; }
        public bool GenerateAxeReports {  get; set; }
        public bool CaptureNetworkTraffic {  get; set; }
        public bool SaveHar { get; set; }
        public PtierVersionModel PtierVersion { get; set; }
        public string ExtraUrls { get; set; }
    }
}
