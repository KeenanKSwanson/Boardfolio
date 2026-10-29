using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Boardfolio.Api.Contracts.Requests;
using Boardfolio.Api.Contracts.Responses;
using Boardfolio.Api.Services;
using Boardfolio.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boardfolio.Api.Controllers;

[ApiController]
[Authorize(Policy = "Account")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[Route("api/builds")]
public class KeyboardBuildController(KeyboardBuildService service) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);


    [HttpGet]
    [ProducesResponseType(typeof(KeyboardBuildListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<KeyboardBuildListResponse>> GetBuilds(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20) => Ok(await service.List(Owner, page, pageSize));


    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(KeyboardBuildResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<KeyboardBuildResponse>> GetBuildById(Guid id) =>
        Ok(new KeyboardBuildResponse(await service.Get(Owner, id)));


    [HttpPost]
    [ProducesResponseType(typeof(KeyboardBuildResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<KeyboardBuildResponse>> CreateBuild(CreateKeyboardBuildRequest request)
    {
        var build = await service.Create(Owner, request);
        return CreatedAtAction(nameof(GetBuildById), new { id = build.Id }, new KeyboardBuildResponse(build));
    }


    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(KeyboardBuildResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<KeyboardBuildResponse>> UpdateBuild(Guid id, UpdateKeyboardBuildRequest request) =>
        Ok(new KeyboardBuildResponse(await service.Update(Owner, id, request)));


    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> DeleteBuild(Guid id)
    {
        await service.Delete(Owner, id);
        return NoContent();
    }


    [HttpPost("{id:guid}/components")]
    [ProducesResponseType(typeof(KeyboardBuildComponent), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> AddComponent(Guid id, ComponentRequest request)
    {
        var component = await service.SaveComponent(Owner, id, null, request);
        return CreatedAtAction(nameof(GetBuildById), new { id }, component);
    }


    [HttpPut("{id:guid}/components/{componentId:guid}")]
    [ProducesResponseType(typeof(KeyboardBuildComponent), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> UpdateComponent(Guid id, Guid componentId, ComponentRequest request) =>
        Ok(await service.SaveComponent(Owner, id, componentId, request));


    [HttpDelete("{id:guid}/components/{componentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> DeleteComponent(Guid id, Guid componentId)
    {
        await service.DeleteComponent(Owner, id, componentId);
        return NoContent();
    }


    [HttpGet("{id:guid}/costs")]
    [ProducesResponseType(typeof(OwnerCostsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<OwnerCostsResponse>> GetCosts(Guid id) => Ok(await service.GetCosts(Owner, id));


    [HttpPut("{id:guid}/costs")]
    [ProducesResponseType(typeof(OwnerCostsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<OwnerCostsResponse>> SaveCosts(Guid id, OwnerCostsRequest request) =>
        Ok(await service.SaveCosts(Owner, id, request));


    [HttpPost("{id:guid}/media")]
    [ProducesResponseType(typeof(BuildMedia), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> AddMedia(Guid id, MediaRequest request)
    {
        var media = await service.AddMedia(Owner, id, request);
        return CreatedAtAction(nameof(GetBuildById), new { id }, media);
    }


    [HttpDelete("{id:guid}/media/{mediaId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> DeleteMedia(Guid id, Guid mediaId)
    {
        await service.DeleteMedia(Owner, id, mediaId);
        return NoContent();
    }
}

