using Boardfolio.Api.Contracts.Requests;
using System.Security.Claims;
using Boardfolio.Api.Contracts.Responses;
using Boardfolio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boardfolio.Api.Controllers;

[ApiController]
[Authorize(Policy = "Account")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[Route("api/me")]
public class AccountBackupController(KeyboardBuildService service) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("import")]
    [ProducesResponseType(typeof(ImportResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> Import(ImportRequest request) => Ok(await service.Import(Owner, request));


    [HttpGet("export")]
    [ProducesResponseType(typeof(ImportRequest), 200)]
    public async Task<IActionResult> Export()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.Export(Owner));
    }
}
