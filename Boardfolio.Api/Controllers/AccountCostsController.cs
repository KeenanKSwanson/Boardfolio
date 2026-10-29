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
public class AccountCostsController(KeyboardBuildService service) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("costs")]
    [ProducesResponseType(typeof(List<OwnerCostsResponse>), 200)]
    public async Task<IActionResult> Costs() => Ok(await service.ListCosts(Owner));


    [HttpGet("cost-totals")]
    [ProducesResponseType(typeof(List<CurrencyTotal>), 200)]
    public async Task<IActionResult> Totals() => Ok(await service.Totals(Owner));
}
