using JobTracker.Core.DTOs;
using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace JobTracker.API.Controllers;

[ApiController]
[Route("api/auth/google")]
public class GoogleAuthController : ControllerBase
{
    private readonly IGoogleOAuthService _googleOAuth;
    private readonly IEmailAccountRepository _accountRepo;
    private readonly IConfiguration _configuration;

    public GoogleAuthController(
        IGoogleOAuthService googleOAuth,
        IEmailAccountRepository accountRepo,
        IConfiguration configuration)
    {
        _googleOAuth = googleOAuth;
        _accountRepo = accountRepo;
        _configuration = configuration;
    }

    [HttpGet("client-id")]
    public ActionResult GetClientId()
    {
        var clientId = _configuration["Google:ClientId"];
        if (string.IsNullOrEmpty(clientId))
            return NotFound(new { error = "Google OAuth is not configured" });

        return Ok(new { clientId });
    }

    [HttpPost("callback")]
    public async Task<ActionResult> HandleCallback([FromBody] GoogleOAuthCallbackDto dto)
    {
        if (string.IsNullOrEmpty(dto.Code))
            return BadRequest(new { error = "Authorization code is required" });

        var account = await _googleOAuth.ExchangeCodeAsync(dto.Code);
        await _accountRepo.CreateOrUpdateAsync(account);

        return Ok(new
        {
            email = account.EmailAddress,
            message = "Gmail account connected successfully"
        });
    }
}
