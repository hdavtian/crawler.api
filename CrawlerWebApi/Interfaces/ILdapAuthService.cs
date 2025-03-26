using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Models;

namespace CrawlerWebApi.Interfaces
{
    public interface ILdapAuthService
    {
        bool AuthenticateUser(string username, string password);
        TaffieUser AuthenticateAndGetUser(string username, string password);
    }
}
