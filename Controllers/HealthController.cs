using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace JustFlip.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly JustFlipDbContext _context; // Palitan ng totoong DbContext class name mo

        public HealthController(JustFlipDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [HttpHead]
        public async Task<IActionResult> GetStatus()
        {
            bool isDbConnected = false;

            try
            {
                // Mabilis na database check (executes SELECT 1)
                isDbConnected = await _context.Database.CanConnectAsync();
            }
            catch
            {
                isDbConnected = false;
            }

            // Kunin ang API Version mula sa Assembly
            var apiVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            var utcNow = DateTime.UtcNow;

            // 2. I-convert sa Philippine Time Zone (UTC+8)
            TimeZoneInfo phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            // Note: Sa Linux/Render, pwede rin ang "Asia/Manila". Para gumana sa parehong Windows at Linux, gamitin ito:

            DateTime phTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow,
                TimeZoneInfo.FindSystemTimeZoneById(
                    OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Manila"
                )
            );

            var statusResult = new
            {
                status = isDbConnected ? "Healthy" : "Degraded",
                version = apiVersion,
                database = isDbConnected ? "Connected" : "Disconnected",
                timestamp = phTime.ToString("MM-dd-yyyy hh:mm tt")
            };


            if (!isDbConnected)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, statusResult);
            }

            return Ok(statusResult);
        }
    }
}
