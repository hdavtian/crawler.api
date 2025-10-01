using CrawlerWebApi.Models;
using CrawlerWebApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CrawlerWebApi.Controllers
{
    [ApiController]
    [Route("api/testmonitor")]
    public class TestMonitorController : ControllerBase
    {
        private readonly TestRegistryService _testRegistryService;

        public TestMonitorController(TestRegistryService testRegistryService)
        {
            _testRegistryService = testRegistryService;
        }

        [HttpGet("running")]
        public ActionResult<IEnumerable<TestStatus>> GetRunningTests()
        {
            return Ok(_testRegistryService.GetAllRunningTests());
        }
    }
}
