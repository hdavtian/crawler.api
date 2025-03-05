using CrawlerWebApi.Models;
using SharpSvn;
using System.Text.Json;

namespace CrawlerWebApi.Services
{
    public class SvnService
    {
        private readonly string _repositoryUrl;
        private readonly string _svnUsername;
        private readonly string _svnPassword;

        public SvnService(IConfiguration configuration)
        {
            var svnSettings = configuration.GetSection("SvnSettings");
            _repositoryUrl = svnSettings["RepositoryUrl"];
            _svnUsername = svnSettings["Username"];
            _svnPassword = svnSettings["Password"];
        }

        public List<SvnCommitLog> GetCommitLogs(string subPath = "", int limit = 10)
        {
            var logs = new List<SvnCommitLog>();

            // Ensure the full path is correctly formatted
            string fullRepoPath = CombineSvnPaths(_repositoryUrl, subPath);

            using (var client = new SvnClient())
            {
                client.Authentication.DefaultCredentials = new System.Net.NetworkCredential(_svnUsername, _svnPassword);

                SvnLogArgs logArgs = new SvnLogArgs
                {
                    Limit = limit
                };

                client.GetLog(new Uri(fullRepoPath), logArgs, out var logItems);

                foreach (var logItem in logItems)
                {
                    logs.Add(new SvnCommitLog
                    {
                        Revision = logItem.Revision,
                        Author = logItem.Author,
                        Message = logItem.LogMessage,
                        Time = logItem.Time
                    });
                }
            }

            return logs;
        }

        private string CombineSvnPaths(string basePath, string additionalPath)
        {
            if (string.IsNullOrWhiteSpace(additionalPath))
                return basePath;

            basePath = basePath.TrimEnd('/');
            additionalPath = additionalPath.TrimStart('/');

            return $"{basePath}/{additionalPath}";
        }
    }
}
