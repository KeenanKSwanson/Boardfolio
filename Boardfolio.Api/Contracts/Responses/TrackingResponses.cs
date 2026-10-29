using Boardfolio.Api.Models;

namespace Boardfolio.Api.Contracts.Responses;

public record OwnerCostsResponse(Guid KeyboardBuildId, string CurrencyCode,
    CostTrackingMethod TrackingMethod, decimal? InitialAmount, List<OwnerCostEntry> Entries,
    string? Notes, decimal TotalPaid);
public record CurrencyTotal(string CurrencyCode, decimal TotalPaid);
public record ImportResponse(int ImportedBuilds, int SkippedBuilds, int ImportedCustomSwitches);
