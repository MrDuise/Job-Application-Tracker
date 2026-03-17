using JobTracker.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailAccountsController : ControllerBase
{
    private readonly IEmailAccountService _accountService;

    public EmailAccountsController(IEmailAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet]
    public async Task<ActionResult> GetAccount()
    {
        var account = await _accountService.GetAccountAsync();
        if (account is null)
            return NotFound();

        return Ok(new
        {
            account.EmailAddress,
            account.LastSyncDate
        });
    }

    [HttpDelete]
    public async Task<ActionResult> Delete()
    {
        await _accountService.DeleteAccountAsync();
        return NoContent();
    }
}
