using System.Security.Claims;
using Boardfolio.Api.Contracts.Requests;
using Boardfolio.Api.Models;
using Boardfolio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boardfolio.Api.Controllers;

[ApiController]
[Authorize(Policy = "Account")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[Route("api/custom-switches")]
public class CustomSwitchesController(KeyboardBuildService service) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);


    [HttpGet]
    [ProducesResponseType(typeof(List<CustomSwitch>), 200)]
    public async Task<IActionResult> Get() => Ok(await service.ListCustomSwitches(Owner));


    [HttpPost]
    [ProducesResponseType(typeof(CustomSwitch), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> Create(CustomSwitchRequest request)
    {
        var recipe = await service.SaveCustomSwitch(Owner, null, request);
        return Created($"/api/custom-switches/{recipe.Id}", recipe);
    }


    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomSwitch), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var recipe = (await service.ListCustomSwitches(Owner)).SingleOrDefault(x => x.Id == id);
        return recipe is null ? NotFound() : Ok(recipe);
    }


    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomSwitch), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Update(Guid id, CustomSwitchRequest request) => Ok(await service.SaveCustomSwitch(Owner, id, request));


    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await service.DeleteCustomSwitch(Owner, id);
        return NoContent();
    }
}
