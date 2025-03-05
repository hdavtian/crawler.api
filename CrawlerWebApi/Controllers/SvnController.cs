using CrawlerWebApi.Models;
using CrawlerWebApi.Services;
using Microsoft.AspNetCore.Mvc;
using SharpSvn;
using System.Collections.Generic;

namespace CrawlerWebApi.Controllers
{
    [Route("api/svn")]
    [ApiController]
    public class SvnController : ControllerBase
    {
        private readonly SvnService _svnService;

        public SvnController(SvnService svnService)
        {
            _svnService = svnService;
        }

        [HttpGet("logs")]
        public ActionResult<List<SvnCommitLog>> GetSvnLogs(
        [FromQuery] string? subPath = null,
        [FromQuery] int limit = 10)
        {
            try
            {
                if (limit <= 0)
                {
                    return BadRequest("Limit must be greater than zero.");
                }

                List<SvnCommitLog> logs = _svnService.GetCommitLogs(subPath, limit);

                if (logs == null || logs.Count == 0)
                {
                    return NotFound($"No logs found for path: {subPath ?? "root"}");
                }

                return Ok(logs);
            }
            catch (SvnFileSystemException ex)
            {
                // Console.WriteLine($"SVN Path Error: {ex.Message}");
                return NotFound($"SVN path not found: {subPath ?? "root"}");
            }
            catch (SvnAuthorizationException ex)
            {
                // Console.WriteLine($"SVN Authentication Error: {ex.Message}");
                return Unauthorized("Invalid SVN credentials or insufficient permissions.");
            }
            catch (SvnException ex)
            {
                // Console.WriteLine($"SVN Error: {ex.Message}");
                return StatusCode(500, "SVN error occurred. Please check logs.");
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Unexpected Error: {ex.Message}");
                return StatusCode(500, "An unexpected error occurred.");
            }
        }
    }
}
