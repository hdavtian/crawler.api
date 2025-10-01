using Microsoft.AspNetCore.Mvc;

namespace CrawlerWebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { 
                status = "healthy", 
                timestamp = DateTime.UtcNow,
                version = "1.0.0",
                service = "CrawlerWebApi"
            });
        }
    }
}