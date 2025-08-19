using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CurrencyConverter.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Public health check endpoint
    /// </summary>
    /// <returns>API health status</returns>
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<object> GetHealth()
    {
        _logger.LogInformation("Health check requested");
        
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            Service = "Currency Converter API",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        });
    }

    /// <summary>
    /// Detailed health check for authenticated users
    /// </summary>
    /// <returns>Detailed API health status</returns>
    [HttpGet("detailed")]
    [Authorize(Roles = "User")]
    public ActionResult<object> GetDetailedHealth()
    {
        _logger.LogInformation("Detailed health check requested by user: {UserId}", 
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            Service = "Currency Converter API",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            DatabaseConnected = true, // In a real app, this would check database connectivity
            ExternalApiStatus = "Available", // In a real app, this would check Frankfurter API
            Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            MemoryUsage = $"{GC.GetTotalMemory(false) / 1024 / 1024} MB",
            RequestedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
        });
    }

    /// <summary>
    /// System information for administrators only
    /// </summary>
    /// <returns>System information</returns>
    [HttpGet("system")]
    [Authorize(Roles = "Admin")]
    public ActionResult<object> GetSystemInfo()
    {
        _logger.LogInformation("System info requested by admin: {UserId}", 
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            SystemInfo = new
            {
                MachineName = Environment.MachineName,
                OSVersion = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = $"{Environment.WorkingSet / 1024 / 1024} MB",
                DotNetVersion = Environment.Version.ToString(),
                ProcessId = Environment.ProcessId
            },
            RequestedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
        });
    }
}