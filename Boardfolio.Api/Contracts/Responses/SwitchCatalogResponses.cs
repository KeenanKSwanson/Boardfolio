using Boardfolio.Api.Models;

namespace Boardfolio.Api.Contracts.Responses;

public record SwitchCatalogResponse(
    List<KeyboardSwitch> Switches,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    string CatalogVersion);

public record SwitchCatalogVersionResponse(string CatalogVersion);
