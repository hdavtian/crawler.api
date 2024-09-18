namespace CrawlerWebApi.Models
{
    public class DiffTestPostRequestModel
    {
        public string BaseTestPath { get; set; }
        public string NewTestPath { get; set; }
        // This is where the diff results will be generated
        public string DiffTestPath {  get; set; }
    }
}
