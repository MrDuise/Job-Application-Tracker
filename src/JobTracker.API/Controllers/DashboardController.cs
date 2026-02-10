using JobTracker.Core.DTOs;
using JobTracker.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IApplicationService _applicationService;

    public DashboardController(IApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
    {
        var stats = await _applicationService.GetDashboardStatsAsync();
        return Ok(stats);
    }
}
