using AngleSharp.Dom;
using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
using CrawlerWebApi.Services;
using IC.Test.Playwright.Crawler.Drivers;
using IC.Test.Playwright.Crawler.Models;
using IC.Test.Playwright.Crawler.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NLog;
using System.Net;

namespace CrawlerWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly IBaselineTestService _baselineTestService;
        private readonly TestModel _testModel;
        private readonly IDiffTestService _diffTestService;
        private readonly ITestService _testService;
        private readonly Logger _logger;

        public TestController(
            ITestService testService,
            IBaselineTestService baselineTestService, 
            IDiffTestService diffTestService,
            TestModel testModel)
        {
            _baselineTestService = baselineTestService;
            _testService = testService;
            _diffTestService = diffTestService;
            _logger = LogManager.GetCurrentClassLogger();
            _testModel = testModel;
        }

        [HttpPost("baseline")]
        public async Task<IActionResult> RunTests([FromBody] BaselineTestPostRequestModel request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            // Generate a unique TestId (GUID) to be used in creating a unique log file per test (for concurrency)
            var testGuid = Guid.NewGuid();
            _testModel.Id = testGuid;
            _testModel.LogFileName = $"crawl-{testGuid}.log";

            using (_logger.PushScopeProperty("TestType", "crawl"))
            using (_logger.PushScopeProperty("TestId", testGuid))
            {
                try
                {
                    // For testing purposes; remove these hardcoded values when deploying
                    
                    request.Url = "https://BostonCommonClientQAUATV4.investcloud.com";
                    request.Username = "client@bostoncommon.com";
                    request.Password = "Mustang.2022";
                    request.Browser = "Chrome";
                    request.Headless = true;
                    request.WindowWidth = 1200;
                    request.WindowHeight = 800;
                    request.RecordVideo = true;
                    request.TakePageScreenshots = true;
                    request.TakeAppScreenshots  = true;
                    request.CaptureAppHtml = true;
                    request.CaptureAppText = true;
                    request.GenerateAxeReports = true;
                    request.CaptureNetworkTraffic = true;
                    request.SaveHar = true;
                    

                    // Return the GUID immediately to the front end
                    Response.StatusCode = (int)HttpStatusCode.OK;
                    await Response.WriteAsync($"{{\"guid\":\"{testGuid}\"}}");

                    // Continue running the test in the background
                    _ = Task.Run(async () =>
                    {
                        var result = await _baselineTestService.RunBaselineTestAsync(request);

                        if (result.Success)
                        {
                            _logger.Info("Crawl operation completed successfully");
                        }
                        else
                        {
                            _logger.Error($"<<Error>> Test run failed: {result.ErrorMessage}");
                        }
                    });

                    // Returning empty result after writing the GUID to response
                    return new EmptyResult(); 
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "<<Error>> An error occurred while running the baseline test.");
                    return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
                }
            }
        }

        [HttpPost("diff")]
        public async Task<IActionResult> RunDiffTest([FromBody] DiffTestPostRequestModel request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            // Generate a unique TestId (GUID) to be used in creating a unique log file per test (for concurrency)
            var testGuid = Guid.NewGuid();
            // Set testModel.Id to generated guid 
            _testModel.Id = testGuid;
            _testModel.LogFileName = $"diff-{testGuid}.log";

            using (_logger.PushScopeProperty("TestType", "diff"))
            using (_logger.PushScopeProperty("TestId", testGuid))
            {
                try
                {
                    // Return the GUID immediately to the front end
                    Response.StatusCode = (int)HttpStatusCode.OK;
                    await Response.WriteAsync($"{{\"guid\":\"{testGuid}\"}}");

                    // for debugging when using swagger, set to proper paths
                    //request.BaseTestPath = @"C:\ictf\crawl-tests\hcglobalpre1\hcglobalpre1\09-25-2024__10-50-26-AM__1440x800";
                    //request.NewTestPath = @"C:\ictf\crawl-tests\hcglobalpre1\hcglobalpre1\09-25-2024__02-28-08-PM__1440x800";

                    var result = await _diffTestService.RunDiffTestAsync(request);

                    if (result.Success)
                    {
                        return Ok(new { message = "Diff operation completed successfully" });
                    }

                    return StatusCode(StatusCodes.Status500InternalServerError, result.ErrorMessage);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "<<Error>> An error occurred while running the diff test.");
                    return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get a specific crawl test by GUID.
        /// </summary>
        /// <param name="guid">The GUID of the crawl test to retrieve.</param>
        /// <returns>The crawl test if found; otherwise, appropriate error response.</returns>
        [HttpGet("crawl-test/{guid}")]
        public async Task<IActionResult> GetCrawlTest(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var testModel = await _testService.GetCrawlTestAsync(guid);

                if (testModel == null)
                {
                    return NotFound(new { message = "Crawl test not found for the provided GUID." });
                }

                return Ok(testModel);
            }
            catch (ArgumentException ex)
            {
                // Direct ArgumentException
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Check for wrapped exceptions
                var errorMessage = ex.InnerException != null
                    ? ex.InnerException.Message
                    : ex.Message;

                _logger.Error(ex, "<<Error>> An error occurred while running the baseline test.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        [HttpGet("crawl-tests/")]
        public async Task<IActionResult> GetCrawlTests()
        {
            try
            {
                var tests = await _testService.GetCrawlTestsAsync();

                if (tests == null)
                {
                    return NotFound(new { message = "Crawl tests not found" });
                }

                return Ok(tests);
            }
            catch (ArgumentException ex)
            {
                // Direct ArgumentException
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Check for wrapped exceptions
                var errorMessage = ex.InnerException != null
                    ? ex.InnerException.Message
                    : ex.Message;

                _logger.Error(ex, "<<Error>> An error occurred while trying to get a list of all crawl tests.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        [HttpGet("crawl-test/page-screenshots/{guid}")]
        public async Task<IActionResult> GetCrawlTestPageScreenshots(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var pageScreenshots = await _testService.GetPageScreenshotsAsync(guid);

                if (pageScreenshots == null)
                {
                    return NotFound(new { message = "Crawl test not found for the provided GUID." });
                }

                return Ok(pageScreenshots);
            }
            catch (ArgumentException ex)
            {
                // Direct ArgumentException
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Check for wrapped exceptions
                var errorMessage = ex.InnerException != null
                    ? ex.InnerException.Message
                    : ex.Message;

                _logger.Error(ex, "<<Error>> An error occurred while running the baseline test.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        [HttpGet("crawl-test/app-screenshots/{guid}")]
        public async Task<IActionResult> GetCrawlTestAppScreenshots(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var appScreenshots = await _testService.GetAppScreenshotsAsync(guid);

                if (appScreenshots == null)
                {
                    return NotFound(new { message = "Crawl test not found for the provided GUID." });
                }

                return Ok(appScreenshots);
            }
            catch (ArgumentException ex)
            {
                // Direct ArgumentException
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Check for wrapped exceptions
                var errorMessage = ex.InnerException != null
                    ? ex.InnerException.Message
                    : ex.Message;

                _logger.Error(ex, "<<Error>> An error occurred while running the baseline test.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        [HttpGet("crawl-test/urls/{guid}")]
        public async Task<IActionResult> GetCrawledUrls(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var items = await _testService.GetCrawledUrlsAsync(guid);

                if (items == null)
                {
                    return NotFound(new { message = "Crawl test urls not found for the provided GUID." });
                }

                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                // Direct ArgumentException
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Check for wrapped exceptions
                var errorMessage = ex.InnerException != null
                    ? ex.InnerException.Message
                    : ex.Message;

                _logger.Error(ex, "<<Error>> An error occurred trying to get crawled urls for baseline test.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        [HttpGet("crawl-test/page-app-summary/{guid}")]
        public async Task<IActionResult> GetPageAppSummary(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var items = await _testService.GetPageAndAppSummaryAsync(guid);

                if (items == null)
                {
                    return NotFound(new { message = "Crawl test page and app summary not found for the provided GUID." });
                }

                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                // Direct ArgumentException
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Check for wrapped exceptions
                var errorMessage = ex.InnerException != null
                    ? ex.InnerException.Message
                    : ex.Message;

                _logger.Error(ex, "<<Error>> An error occurred trying to get page and app summary for baseline test.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        [HttpGet("crawl-test/app-artifacts/{guid}")]
        public async Task<IActionResult> GetAppArtifacts(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var items = await _testService.GetAppArtifactsAsync(guid);

                if (items == null)
                {
                    return NotFound(new { message = "Crawl test app artifacts not found for the provided GUID." });
                }

                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                // Direct ArgumentException
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Check for wrapped exceptions
                var errorMessage = ex.InnerException != null
                    ? ex.InnerException.Message
                    : ex.Message;

                _logger.Error(ex, "<<Error>> An error occurred trying to get app artifacts for baseline test.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }
    }
}
