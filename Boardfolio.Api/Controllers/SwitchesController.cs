using System.ComponentModel.DataAnnotations;
using Boardfolio.Api.Contracts.Responses;
using Boardfolio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Boardfolio.Api.Controllers;

[ApiController]
[Route("api/switches")]
public class SwitchesController(KeyboardBuildService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SwitchCatalogResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<SwitchCatalogResponse>> Get(
        [FromQuery] string? search,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 200)] int pageSize = 200,
        [FromQuery, RegularExpression("^[0-9a-f]{64}$")] string? catalogVersion = null,
        CancellationToken cancellationToken = default) =>
        Ok(await service.ListSwitches(search, page, pageSize, catalogVersion, cancellationToken));

    [HttpGet("version")]
    [ProducesResponseType(typeof(SwitchCatalogVersionResponse), 200)]
    public async Task<ActionResult<SwitchCatalogVersionResponse>> GetVersion(CancellationToken cancellationToken) =>
        Ok(new SwitchCatalogVersionResponse(await service.GetSwitchCatalogVersion(cancellationToken)));
}
