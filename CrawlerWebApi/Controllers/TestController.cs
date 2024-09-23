using AngleSharp.Dom;
using CrawlerWebApi.Interfaces;
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
        private readonly IBaselineTestService _baselineTestService;
        private readonly IDiffTestService _diffTestService;
        private readonly Logger _logger;

        public TestController(IBaselineTestService baselineTestService, IDiffTestService diffTestService)
        {
            _baselineTestService = baselineTestService;
            _diffTestService = diffTestService;
            _logger = LogManager.GetCurrentClassLogger();
        }

        [HttpPost("baseline")]
        public async Task<IActionResult> RunTests([FromBody] BaselineTestPostRequestModel request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            try
            {
                // delete this
                //request.Url = "https://BostonCommonClientQAUATV4.investcloud.com";
                //request.Username = "client@bostoncommon.com";
                //request.Password = "Mustang.2022";

                // Delegate the entire logic to TestService while keeping all functionality intact
                var result = await _baselineTestService.RunBaselineTestAsync(request);

                if (result.Success)
                {
                    return Ok(new { message = "Crawl operation completed successfully" });
                }

                return StatusCode(StatusCodes.Status500InternalServerError, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An error occurred while running the baseline test.");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
            }
        }

        [HttpPost("diff")]
        public async Task<IActionResult> RunDiffTest([FromBody] DiffTestPostRequestModel request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request");
            }

            try
            {
                // delete this
                request.BaseTestPath = @"C:\ictf\crawl-tests\bostoncommonclientqauatv4\bostoncommonclientqauatv4\09-18-2024__05-29-31-AM__1600x1000";
                request.NewTestPath = @"C:\ictf\crawl-tests\bostoncommonclientqauatv4\bostoncommonclientqauatv4\09-18-2024__08-12-01-AM__1600x1000";
                
                // Delegate the entire logic to TestService while keeping all functionality intact
                var result = await _diffTestService.RunDiffTestAsync(request);

                if (result.Success)
                {
                    return Ok(new { message = "Crawl operation completed successfully" });
                }

                return StatusCode(StatusCodes.Status500InternalServerError, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An error occurred while running the baseline test.");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
            }
        }
    }
}
