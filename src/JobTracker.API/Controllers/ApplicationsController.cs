using JobTracker.Core.DTOs;
using JobTracker.Core.Enums;
using JobTracker.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _applicationService;

    public ApplicationsController(IApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ApplicationDto>>> GetAll()
    {
        var applications = await _applicationService.GetAllApplicationsAsync();
        return Ok(applications);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApplicationDto>> GetById(int id)
    {
        var application = await _applicationService.GetApplicationByIdAsync(id);
        if (application is null)
            return NotFound();
        return Ok(application);
    }

    [HttpGet("status/{status}")]
    public async Task<ActionResult<List<ApplicationDto>>> GetByStatus(ApplicationStatus status)
    {
        var applications = await _applicationService.GetApplicationsByStatusAsync(status);
        return Ok(applications);
    }

    [HttpPost]
    public async Task<ActionResult<ApplicationDto>> Create([FromBody] CreateApplicationDto dto)
    {
        var application = await _applicationService.CreateApplicationAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = application.Id }, application);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApplicationDto>> Update(int id, [FromBody] UpdateApplicationDto dto)
    {
        try
        {
            var application = await _applicationService.UpdateApplicationAsync(id, dto);
            return Ok(application);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPatch("{id}/status")]
    public async Task<ActionResult> UpdateStatus(int id, [FromBody] ApplicationStatus status)
    {
        try
        {
            await _applicationService.UpdateApplicationStatusAsync(id, status);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/notes")]
    public async Task<ActionResult> AddNote(int id, [FromBody] string note)
    {
        try
        {
            await _applicationService.AddNoteToApplicationAsync(id, note);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            await _applicationService.DeleteApplicationAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
