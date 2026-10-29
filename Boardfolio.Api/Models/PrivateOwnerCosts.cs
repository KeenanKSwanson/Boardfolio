using System.ComponentModel.DataAnnotations;

namespace Boardfolio.Api.Models;

// An owner's private ledger, deliberately not a navigation property on KeyboardBuild.
// Keep this record with its original owner when the keyboard changes hands.
public class PrivateOwnerCosts : IValidatableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid KeyboardBuildId { get; set; }

    // Null only for device-local records. Server storage must require the signed-in owner.
    public Guid? OwnerUserId { get; set; }

    [Required, RegularExpression("^[A-Z]{3}$")]
    public string CurrencyCode { get; set; } = "USD";

    [EnumDataType(typeof(CostTrackingMethod))]
    public CostTrackingMethod TrackingMethod { get; set; } = CostTrackingMethod.Itemized;

    // Purchase price or total initial build cost; used only in OverallAmount mode.
    public decimal? InitialAmount { get; set; }

    public List<OwnerCostEntry> Entries { get; set; } = [];

    [StringLength(2000)]
    public string? Notes { get; set; }

    // Initial itemized entries can be retained as a breakdown without double counting.
    public decimal TotalPaid =>
        (TrackingMethod == CostTrackingMethod.OverallAmount
            ? InitialAmount ?? 0m
            : Entries.Where(entry => entry.Stage == CostStage.InitialBuild).Sum(entry => entry.Amount))
        + Entries.Where(entry => entry.Stage == CostStage.LaterSpending).Sum(entry => entry.Amount);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (KeyboardBuildId == Guid.Empty)
            yield return new ValidationResult("A build ID is required.", [nameof(KeyboardBuildId)]);

        if (OwnerUserId == Guid.Empty)
            yield return new ValidationResult("An owner ID cannot be empty.", [nameof(OwnerUserId)]);

        if (TrackingMethod == CostTrackingMethod.OverallAmount &&
            (InitialAmount is null || InitialAmount < 0))
            yield return new ValidationResult("An overall amount of zero or more is required.", [nameof(InitialAmount)]);

        if (TrackingMethod == CostTrackingMethod.Itemized && InitialAmount is not null)
            yield return new ValidationResult("Use entries instead of an overall amount for itemized tracking.", [nameof(InitialAmount)]);
    }
}

public class OwnerCostEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Optional reference only; deleting or replacing a component must retain its expense.
    public Guid? BuildComponentId { get; set; }

    [Required, StringLength(200)]
    public string Description { get; set; } = string.Empty;

    // Total paid for this entry, including all units; not a per-unit price.
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [EnumDataType(typeof(CostStage))]
    public CostStage Stage { get; set; } = CostStage.InitialBuild;

    public DateOnly? PaidOn { get; set; }
}

public enum CostTrackingMethod
{
    Itemized = 0,
    OverallAmount = 1
}

public enum CostStage
{
    InitialBuild = 0,
    LaterSpending = 1
}
