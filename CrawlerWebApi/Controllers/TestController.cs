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
    [Route("api/tests")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly IBaselineTestService BaselineTestService;
        private readonly CrawlTest CrawlTest;
        private readonly DiffTest DiffTest;
        private readonly IDiffTestService DiffTestService;
        private readonly ITestService TestService;
        private readonly Logger Logger;

        public TestController(
            ITestService testService,
            IBaselineTestService baselineTestService, 
            IDiffTestService diffTestService,
            CrawlTest testModel,
            DiffTest diffTest)
        {
            BaselineTestService = baselineTestService;
            TestService = testService;
            DiffTestService = diffTestService;
            Logger = LogManager.GetCurrentClassLogger();
            CrawlTest = testModel;
            DiffTest = diffTest;
        }
        
        //
        // -
        // --
        // ---
        // ----
        // test launch endpoints
        // ----
        // ---
        // --
        // -
        //
        
        [HttpPost("crawl-tests/launch")]
        public async Task<IActionResult> RunTests([FromBody] BaselineTestPostRequestModel request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            // Generate a unique TestId (GUID) to be used in creating a unique log file per test (for concurrency)
            var testGuid = Guid.NewGuid();
            CrawlTest.Id = testGuid;
            CrawlTest.LogFileName = $"crawl-{testGuid}.log";

            using (Logger.PushScopeProperty("TestType", "crawl"))
            using (Logger.PushScopeProperty("TestId", testGuid))
            {
                try
                {
                    // For testing purposes; remove these hardcoded values when deploying
                    /*
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
                    */
                    // Return the GUID immediately to the front end
                    Response.StatusCode = (int)HttpStatusCode.OK;
                    await Response.WriteAsync($"{{\"testGuid\":\"{testGuid}\"}}");

                    // Continue running the test in the background
                    _ = Task.Run(async () =>
                    {
                        var result = await BaselineTestService.RunBaselineTestAsync(request);

                        if (result.Success)
                        {
                            Logger.Info("Crawl operation completed successfully");
                        }
                        else
                        {
                            Logger.Error($"<<Error>> Test run failed: {result.ErrorMessage}");
                        }
                    });

                    // Returning empty result after writing the GUID to response
                    return new EmptyResult(); 
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> An error occurred while running the baseline test.");
                    return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
                }
            }
        }

        [HttpPost("diff-tests/launch")]
        public async Task<IActionResult> RunDiffTest([FromBody] DiffTestPostRequestModel request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            // Generate a unique TestId (GUID) to be used in creating a unique log file per test (for concurrency)
            var testGuid = Guid.NewGuid();
            DiffTest.Id = testGuid;
            DiffTest.LogFileName = $"diff-{testGuid}.log";

            using (Logger.PushScopeProperty("TestType", "diff"))
            using (Logger.PushScopeProperty("TestId", testGuid))
            {
                try
                {
                    // Return the GUID immediately to the front end
                    Response.StatusCode = (int)HttpStatusCode.OK;
                    await Response.WriteAsync($"{{\"testGuid\":\"{testGuid}\"}}");

                    var result = await DiffTestService.RunDiffTestAsync(request);

                    if (result.Success)
                    {
                        return Ok(new { message = "Diff operation completed successfully" });
                    }

                    return StatusCode(StatusCodes.Status500InternalServerError, result.ErrorMessage);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> An error occurred while running the diff test.");
                    return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
                }
            }
        }
        
        //
        // -
        // --
        // ---
        // ----
        // crawl test endpoints
        // ----
        // ---
        // --
        // -
        //
        
        [HttpGet("crawl-tests")]
        public async Task<IActionResult> GetCrawlTests()
        {
            try
            {
                var tests = await TestService.GetCrawlTests();

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

                Logger.Error(ex, "<<Error>> An error occurred while trying to get a list of all crawl tests.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        [HttpGet("crawl-tests/{testGuid}")]
        public async Task<IActionResult> GetCrawlTest(string testGuid)
        {
            if (string.IsNullOrWhiteSpace(testGuid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var testModel = await TestService.GetCrawlTest(testGuid);

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

                Logger.Error(ex, "<<Error>> An error occurred while running the baseline test.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        [HttpGet("crawl-tests/{testGuid}/page-screenshots")]
        public async Task<IActionResult> GetCrawlTestPageScreenshots(string testGuid)
        {
            if (string.IsNullOrWhiteSpace(testGuid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var pageScreenshots = await TestService.GetPageScreenshots(testGuid);

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

                Logger.Error(ex, "<<Error>> An error occurred while running the baseline test.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        [HttpGet("crawl-tests/{testGuid}/app-screenshots")]
        public async Task<IActionResult> GetCrawlTestAppScreenshots(string testGuid)
        {
            if (string.IsNullOrWhiteSpace(testGuid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var appScreenshots = await TestService.GetAppScreenshots(testGuid);

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

                Logger.Error(ex, "<<Error>> An error occurred while running the baseline test.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        [HttpGet("crawl-tests/{testGuid}/urls")]
        public async Task<IActionResult> GetCrawledUrls(string testGuid)
        {
            if (string.IsNullOrWhiteSpace(testGuid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var items = await TestService.GetCrawledUrls(testGuid);

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

                Logger.Error(ex, "<<Error>> An error occurred trying to get crawled urls for baseline test.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        [HttpGet("crawl-tests/{testGuid}/page-app-summary")]
        public async Task<IActionResult> GetPageAppSummary(string testGuid)
        {
            if (string.IsNullOrWhiteSpace(testGuid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var items = await TestService.GetPageAndAppSummary(testGuid);

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

                Logger.Error(ex, "<<Error>> An error occurred trying to get page and app summary for baseline test.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        [HttpGet("crawl-tests/{testGuid}/app-artifacts")]
        public async Task<IActionResult> GetAppArtifacts(string testGuid)
        {
            if (string.IsNullOrWhiteSpace(testGuid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var items = await TestService.GetAppArtifacts(testGuid);

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

                Logger.Error(ex, "<<Error>> An error occurred trying to get app artifacts for baseline test.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        [HttpGet("crawl-tests/{testGuid}/app-html/{appGuid}")]
        public async Task<IActionResult> GetAppHtml(string testGuid, string appGuid)
        {
            if (string.IsNullOrWhiteSpace(testGuid))
                return BadRequest(new { message = "Test Guid parameter cannot be null or empty." });

            if (string.IsNullOrWhiteSpace(appGuid))
                return BadRequest(new { message = "App Guid parameter cannot be null or empty." });

            try
            {
                string content = await TestService.GetAppHtml(testGuid, appGuid);

                if (content == null)
                {
                    return NotFound(new { message = "App html was not found" });
                }

                return Ok(content);
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

                Logger.Error(ex, "<<Error>> An error occurred trying to get app html");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }

        //
        // -
        // --
        // ---
        // ----
        // diff test endpoints
        // ----
        // ---
        // --
        // -
        //

        [HttpGet("diff-tests")]
        public async Task<IActionResult> GetDiffTests()
        {
            try
            {
                var tests = await TestService.GetDiffTests();

                if (tests == null)
                {
                    return NotFound(new { message = "Diff tests not found" });
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

                Logger.Error(ex, "<<Error>> An error occurred while trying to get a list of all diff tests.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }
        [HttpGet("diff-tests/{testGuid}")]
        public async Task<IActionResult> GetDiffTest(string testGuid)
        {
            if (string.IsNullOrWhiteSpace(testGuid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var testModel = await TestService.GetDiffTest(testGuid);

                if (testModel == null)
                {
                    return NotFound(new { message = "Diff test not found for the provided GUID." });
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

                Logger.Error(ex, "<<Error>> An error occurred while getting the diff.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }
        [HttpGet("diff-tests/{testGuid}/app-diffs")]
        public async Task<IActionResult> GetAllAppDiffs(string testGuid)
        {
            if (string.IsNullOrWhiteSpace(testGuid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var appDiffs = await TestService.GetAllAppDiffs(testGuid);

                if (appDiffs == null)
                {
                    return NotFound(new { message = "App diffs not found for the provided GUID." });
                }

                return Ok(appDiffs);
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

                Logger.Error(ex, "<<Error>> An error occurred while getting app diffs.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }
        [HttpGet("diff-tests/{testGuid}/app-diffs/{appGuid}")]
        public async Task<IActionResult> GetAppDiffs(string testGuid, string appGuid)
        {
            if (string.IsNullOrWhiteSpace(testGuid))
                return BadRequest(new { message = "Test guid parameter cannot be null or empty." });

            if (string.IsNullOrWhiteSpace(appGuid))
                return BadRequest(new { message = "App guid parameter cannot be null or empty." });

            try
            {
                var appDiffs = await TestService.GetAppDiffs(testGuid, appGuid);

                if (appDiffs == null)
                {
                    return NotFound(new { message = "App diffs not found for the provided GUID." });
                }

                return Ok(appDiffs);
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

                Logger.Error(ex, "<<Error>> An error occurred while getting app diffs.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }
        [HttpGet("diff-tests/{testGuid}/page-screenshot-diffs")]
        public async Task<IActionResult> GetAllPageScreenshotDiffs(string testGuid)
        {
            if (string.IsNullOrWhiteSpace(testGuid))
                return BadRequest(new { message = "Guid parameter cannot be null or empty." });

            try
            {
                var appDiffs = await TestService.GetAllPageScreenshotDiffs(testGuid);

                if (appDiffs == null)
                {
                    return NotFound(new { message = "Page screenshot diffs not found for the provided GUID." });
                }

                return Ok(appDiffs);
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

                Logger.Error(ex, "<<Error>> An error occurred while getting page screenshot diffs.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }
        [HttpGet("diff-tests/{testGuid}/page-screenshot-diffs/{pageGuid}")]
        public async Task<IActionResult> GetPageScreenshotDiffs(string testGuid, string pageGuid)
        {
            if (string.IsNullOrWhiteSpace(testGuid))
                return BadRequest(new { message = "Test guid parameter cannot be null or empty." });

            if (string.IsNullOrWhiteSpace(pageGuid))
                return BadRequest(new { message = "Page guid parameter cannot be null or empty." });

            try
            {
                var pageScreenshotDiff = await TestService.GetPageScreenshotDiff(testGuid, pageGuid);

                if (pageScreenshotDiff == null)
                {
                    return NotFound(new { message = "Page screenshot diffs not found for the provided GUID." });
                }

                return Ok(pageScreenshotDiff);
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

                Logger.Error(ex, "<<Error>> An error occurred while getting page screenshot diffs.");
                return StatusCode(500, new { message = "An unexpected error occurred.", details = errorMessage });
            }
        }
        // --------------------------------
        // Other
        // --------------------------------
        [HttpGet("proxy/ptier-version")]
        public async Task<IActionResult> GetVersions([FromQuery] string url)
        {
            var endpoint = "https://denpwptool1.investcloud.int/api/release-manager/find-versions?url=" + Uri.EscapeDataString(url);

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(endpoint);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                var content = await response.Content.ReadAsStringAsync();

                // Deserialize using Newtonsoft.Json
                var model = Newtonsoft.Json.JsonConvert.DeserializeObject<PtierVersionModel>(content);

                return Ok(model);
            }
        }
    }
}
