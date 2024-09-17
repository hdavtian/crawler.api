using AngleSharp.Dom;
using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Drivers;
using IC.Test.Playwright.Crawler.Models;
using IC.Test.Playwright.Crawler.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using NLog;

namespace CrawlerWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly PlaywrightContext _playwrightContext;
        private readonly LoginDriver _loginDriver;
        private readonly TestModel _testModel;
        private readonly CrawlDriver _crawlDriver;
        private readonly Logger _logger;

        public TestController(
            PlaywrightContext playwrightContext,
            LoginDriver loginDriver,
            TestModel testModel,
            CrawlDriver crawlDriver
        )
        {
            _playwrightContext = playwrightContext;
            _loginDriver = loginDriver;
            _testModel = testModel;
            _crawlDriver = crawlDriver;
            _logger = NLog.LogManager.GetCurrentClassLogger();
        }

        [HttpPost("baseline")]
        public async Task<IActionResult> RunTests([FromBody] BaselineTestPostRequestModel request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            // Access the data from the request
            string url = request.Url;
            string username = request.Username;
            string password = request.Password;

            // Assign values from the request to your TestModel (adjust as necessary)
            TimerUtil.StartTimer(_testModel.Timers, "ScenarioDuration");
            _testModel.Name = "Baseline test: " + url;
            _testModel.Description = "Some description";
            _testModel.DateTime = DateTime.Now;
            _testModel.Browser.Width = 1600;
            _testModel.Browser.Height = 1000;

            // Create the HAR file path
            string _harFileName = $"{_testModel.Id}.har";
            string _harFileOriginalPath = Path.Combine(@"C:\ictf", _harFileName);

            // Initialize PlaywrightContext with the HAR path
            await _playwrightContext.InitializeAsync(_harFileOriginalPath);

            // Assign browser details to TestModel
            _testModel.Browser.Name = _playwrightContext.BrowserName;

            // Network interception setup
            List<NetworkData> _networkData = new List<NetworkData>();
            var page = _playwrightContext.Page;  // Access the initialized Page
            string _currentPageUrl = page.Url;

            page.FrameNavigated += (_, frame) =>
            {
                if (frame == page.MainFrame)
                {
                    _currentPageUrl = frame.Url;
                    _logger.Info($"Navigated to: {_currentPageUrl}");
                }
            };

            // Intercept network requests and responses
            page.Request += (_, request) =>
            {
                _logger.Info($"Request intercepted: {request.Url}");
                _networkData.Add(new NetworkData
                {
                    Url = request.Url,
                    Method = request.Method,
                    Headers = request.Headers,
                    PostData = request.PostData,
                    PageUrl = _currentPageUrl
                });
            };

            page.Response += async (_, response) =>
            {
                _logger.Info($"Response intercepted: {response.Url} with status {response.Status}");
                var matchingRequest = _networkData.FirstOrDefault(r => r.Url == response.Url);
                if (matchingRequest != null)
                {
                    try
                    {
                        // Ensure status code is captured correctly
                        matchingRequest.StatusCode = response.Status;

                        // Check if the response is a redirect
                        _logger.Info("Skipping response capture to avoid decoding issues and gibberish in data.");
                    }
                    catch (PlaywrightException ex)
                    {
                        _logger.Error($"Failed to read response body: {ex.Message}");
                    }
                }
            };

            try
            {
                // Perform login via LoginDriver
                await _loginDriver.LoginToApplication(url, username, password);

                // Crawl operation (currently commented out)
                await _crawlDriver.Crawl(_testModel.BaseSaveFolder, _testModel.BaseUrl);

                // Return success response if no exceptions
                return Ok("Crawl operation completed successfully");
            }
            catch (Exception ex)
            {
                // Return error if an exception occurs
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
            }
        }
    }
}
