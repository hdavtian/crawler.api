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
        private readonly ITestService _testService;
        private readonly Logger _logger;

        public TestController(ITestService testService)
        {
            _testService = testService;
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
                // Delegate the entire logic to TestService while keeping all functionality intact
                var result = await _testService.RunBaselineTestAsync(request);

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
