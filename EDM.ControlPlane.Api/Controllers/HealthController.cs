using System;
using Microsoft.AspNetCore.Mvc;
using EDM.ControlPlane.Api.Data;

namespace EDM.ControlPlane.Api.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly ControlPlaneDbContext _dbContext;

        public HealthController(ControlPlaneDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public IActionResult GetHealth()
        {
            return Ok(new
            {
                status = "Healthy",
                service = "EDM.ControlPlane.Api",
                version = "2.0.0",
                timestampUtc = DateTime.UtcNow
            });
        }

        [HttpGet("ready")]
        public async Task<IActionResult> GetReadiness()
        {
            bool dbConnected = await _dbContext.Database.CanConnectAsync();
            if (dbConnected)
            {
                return Ok(new { status = "Ready", database = "Connected", timestampUtc = DateTime.UtcNow });
            }
            return StatusCode(503, new { status = "Unhealthy", database = "Disconnected", timestampUtc = DateTime.UtcNow });
        }

        [HttpGet("live")]
        public IActionResult GetLiveness()
        {
            return Ok(new { status = "Alive", timestampUtc = DateTime.UtcNow });
        }
    }
}
