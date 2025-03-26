using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Models;
using Microsoft.AspNetCore.Authentication;
using Novell.Directory.Ldap;
using System.DirectoryServices;

namespace CrawlerWebApi.Services
{
    public class LdapAuthService : ILdapAuthService
    {
        private readonly IConfiguration _config;

        public LdapAuthService(IConfiguration config)
        {
            _config = config;
        }
        
        public bool AuthenticateUser(string username, string password)
        {
            string ldapPath = _config["LDAP:LDAPUrl"];
            try
            {
                // Create a DirectoryEntry, passing user creds directly
                // This attempts to bind with "username" and "password"
                using (DirectoryEntry entry = new DirectoryEntry(ldapPath, username, password))
                {
                    // Accessing NativeObject forces a bind, throwing an error if invalid
                    var obj = entry.NativeObject;
                }

                // If we reach here, authentication succeeded
                return true;
            }
            catch
            {
                // Any DirectoryServices exception => invalid credentials, locked out, or server error
                return false;
            }
        }

        public TaffieUser AuthenticateAndGetUser(string username, string password)
        {
            // this is for testing
            if (false)
            {
                var tempTestUser = new TaffieUser
                {
                    Authenticated = true,
                    Username = username,
                    DisplayName = "Harma Davtian",
                    DistinguishedName = "Harma Davtian",
                    Email = "hdavtian@investcloud.com",
                };
                return tempTestUser;
            }
            
            string ldapPath = _config["LDAP:LDAPUrl"];
            try
            {
                // Create a DirectoryEntry, passing user creds directly
                // This attempts to bind with "username" and "password"
                using (DirectoryEntry entry = new DirectoryEntry(ldapPath, username, password))
                {
                    // Accessing NativeObject forces a bind, throwing an error if invalid
                    var obj = entry.NativeObject;

                    // If we reach here, authentication succeeded
                    using (System.DirectoryServices.DirectorySearcher searcher =
                       new System.DirectoryServices.DirectorySearcher(entry)
                       {
                           Filter = $"(sAMAccountName={username})"
                       })
                    {
                        searcher.PropertiesToLoad.Add("displayName");
                        searcher.PropertiesToLoad.Add("mail");
                        searcher.PropertiesToLoad.Add("distinguishedName");

                        var result = searcher.FindOne();
                        if (result == null) return null;

                        using (System.DirectoryServices.DirectoryEntry userEntry = result.GetDirectoryEntry())
                        {
                            var taffieUser = new TaffieUser
                            {
                                Authenticated = true,
                                Username = username,
                                DisplayName = userEntry.Properties["displayName"]?.Value?.ToString(),
                                DistinguishedName = userEntry.Properties["distinguishedName"]?.Value?.ToString(),
                                Email = userEntry.Properties["mail"]?.Value?.ToString(),
                            };
                            return taffieUser;
                        }
                    }
                }
            }
            catch
            {
                // Any DirectoryServices exception => invalid credentials, locked out, or server error
                return null;
            }
        }

    }
}
