using IC.Test.Playwright.Crawler.Models;

namespace CrawlerWebApi.Models
{
    public class DiffTestPostRequestModel
    {
        public string? BaseTestId { get; set; }
        public string? NewTestId { get; set; }
        public TaffieUser? TaffieUser { get; set; }
    }
}
