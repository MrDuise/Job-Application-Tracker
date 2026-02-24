using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Models;
using JobTracker.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailsController : ControllerBase
{
    private readonly IEmailRepository _emailRepo;
    private readonly EmailSyncOrchestrator _syncOrchestrator;

    public EmailsController(
        IEmailRepository emailRepo,
        EmailSyncOrchestrator syncOrchestrator)
    {
        _emailRepo = emailRepo;
        _syncOrchestrator = syncOrchestrator;
    }

    [HttpGet]
    public async Task<ActionResult<List<Email>>> GetAll()
    {
        var emails = await _emailRepo.GetAllAsync();
        return Ok(emails);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Email>> GetById(string id)
    {
        var email = await _emailRepo.GetByIdAsync(id);
        if (email is null)
            return NotFound();
        return Ok(email);
    }

    [HttpGet("application/{appId}")]
    public async Task<ActionResult<List<Email>>> GetByApplication(int appId)
    {
        var emails = await _emailRepo.GetByApplicationIdAsync(appId);
        return Ok(emails);
    }

    [HttpPost("sync")]
    public ActionResult TriggerSync()
    {
        if (!_syncOrchestrator.TryStartSync())
            return Conflict(new { message = "Sync already in progress", status = _syncOrchestrator.GetStatus() });

        return Accepted(new { message = "Sync started", status = _syncOrchestrator.GetStatus() });
    }

    [HttpGet("sync/status")]
    public ActionResult GetSyncStatus()
    {
        return Ok(_syncOrchestrator.GetStatus());
    }

    [HttpPatch("{id}/link")]
    public async Task<ActionResult> LinkToApplication(string id, [FromBody] int applicationId)
    {
        try
        {
            await _emailRepo.LinkToApplicationAsync(id, applicationId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
