namespace CrawlerWebApi.Models
{
    public class LDAPUserInfo
    {
        public bool Authenticated { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string DistinguishedName { get; set; }
        public string Email { get; set; }
    }
}
