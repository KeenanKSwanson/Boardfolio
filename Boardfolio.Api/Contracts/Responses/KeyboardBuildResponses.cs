using Boardfolio.Api.Models;

namespace Boardfolio.Api.Contracts.Responses
{
    public record KeyboardBuildResponse(KeyboardBuild KeyboardBuild);

    public record KeyboardBuildListResponse(
        List<KeyboardBuild> KeyboardBuilds, 
        int Page, 
        int PageSize, 
        int TotalCount, 
        int TotalPages
    );
}
