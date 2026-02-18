using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using JobTracker.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailsController : ControllerBase
{
    private readonly IEmailRepository _emailRepo;
    private readonly IEmailAccountRepository _accountRepo;
    private readonly IEmailService _emailService;
    private readonly IEmailProcessingService _processingService;

    public EmailsController(
        IEmailRepository emailRepo,
        IEmailAccountRepository accountRepo,
        IEmailService emailService,
        IEmailProcessingService processingService)
    {
        _emailRepo = emailRepo;
        _accountRepo = accountRepo;
        _emailService = emailService;
        _processingService = processingService;
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
    public async Task<ActionResult> TriggerSync()
    {
        var account = await _accountRepo.GetAccountAsync();
        if (account is null)
            return BadRequest("No email account configured.");

        _emailService.Configure(account);

        var since = account.LastSyncDate ?? DateTime.UtcNow.AddDays(-548);
        var emails = await _emailService.FetchEmailsSinceAsync(since);
        await _processingService.ProcessEmailBatchAsync(emails);

        account.LastSyncDate = DateTime.UtcNow;
        await _accountRepo.CreateOrUpdateAsync(account);

        return Ok(new { processed = emails.Count });
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
