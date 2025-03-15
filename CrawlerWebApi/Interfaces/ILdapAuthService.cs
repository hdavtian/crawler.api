using CrawlerWebApi.Models;

namespace CrawlerWebApi.Interfaces
{
    public interface ILdapAuthService
    {
        bool AuthenticateUser(string username, string password);
        LDAPUserInfo AuthenticateAndGetUser(string username, string password);
    }
}
