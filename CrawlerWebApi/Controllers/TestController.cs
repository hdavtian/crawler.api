using AngleSharp.Dom;
using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
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
        private readonly Logger _logger;

        public TestController(
            IBaselineTestService baselineTestService, 
            IDiffTestService diffTestService,
            TestModel testModel)
        {
            _baselineTestService = baselineTestService;
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
                    /*
                    request.Url = "https://BostonCommonClientQAUATV4.investcloud.com";
                    request.Username = "client@bostoncommon.com";
                    request.Password = "Mustang.2022";
                    request.Browser = "Chrome";
                    request.Headless = false;
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
    }
}
