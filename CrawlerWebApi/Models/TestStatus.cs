namespace CrawlerWebApi.Models
{
    public class TestStatus
    {
        public Guid TestId { get; set; }
        public string TestType { get; set; } // "Crawl", "Diff", etc.
        public DateTime StartTime { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? TriggeredBy { get; set; }
    }
}
