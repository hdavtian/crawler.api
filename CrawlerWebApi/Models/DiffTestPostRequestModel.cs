namespace CrawlerWebApi.Models
{
    public class DiffTestPostRequestModel
    {
        public string BaseTestPath { get; set; }
        public string NewTestPath { get; set; }
        public Guid BaseTestId { get; set; }
        public Guid NewTestId { get; set; }
    }
}
