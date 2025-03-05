namespace CrawlerWebApi.Models
{
    public class SvnCommitLog
    {
        public long Revision { get; set; }
        public string Author { get; set; }
        public string Message { get; set; }
        public DateTime Time { get; set; }
    }
}
