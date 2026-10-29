using System.ComponentModel.DataAnnotations;
using Boardfolio.Api.Models;

namespace Boardfolio.Api.Contracts.Requests;

public class ComponentRequest : IValidatableObject
{
    [EnumDataType(typeof(ComponentType))]
    public ComponentType Type { get; set; }
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;
    [StringLength(100)]
    public string? Manufacturer { get; set; }
    [Range(1, 10000)]
    public int Quantity { get; set; } = 1;
    public Guid? KeyboardSwitchId { get; set; }
    public Guid? CustomSwitchId { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        new KeyboardBuildComponent { Type = Type, KeyboardSwitchId = KeyboardSwitchId, CustomSwitchId = CustomSwitchId }
            .Validate(context);
}

public class CustomSwitchRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;
    [EnumDataType(typeof(SwitchType))]
    public SwitchType Type { get; set; }
    [Required, StringLength(150)]
    public string TopHousing { get; set; } = string.Empty;
    [Required, StringLength(150)]
    public string BottomHousing { get; set; } = string.Empty;
    [Required, StringLength(150)]
    public string Stem { get; set; } = string.Empty;
    [Required, StringLength(150)]
    public string Spring { get; set; } = string.Empty;
    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class OwnerCostsRequest : IValidatableObject
{
    [Required, RegularExpression("^[A-Z]{3}$")]
    public string CurrencyCode { get; set; } = "USD";
    [EnumDataType(typeof(CostTrackingMethod))]
    public CostTrackingMethod TrackingMethod { get; set; }
    [Range(typeof(decimal), "0", "1000000000000")]
    public decimal? InitialAmount { get; set; }
    [Required, MaxLength(1000)]
    public List<CostEntryRequest> Entries { get; set; } = [];
    [StringLength(2000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (TrackingMethod == CostTrackingMethod.OverallAmount && (InitialAmount is null or < 0))
            yield return new ValidationResult("An overall amount of zero or more is required.", [nameof(InitialAmount)]);
        if (TrackingMethod == CostTrackingMethod.Itemized && InitialAmount is not null)
            yield return new ValidationResult("Use entries for itemized tracking.", [nameof(InitialAmount)]);
        if (Entries?.Any(x => x is null) == true)
            yield return new ValidationResult("Cost entries cannot be null.", [nameof(Entries)]);
    }
}

public class CostEntryRequest
{
    public Guid? BuildComponentId { get; set; }
    [Required, StringLength(200)]
    public string Description { get; set; } = string.Empty;
    [Range(typeof(decimal), "0", "1000000000000")]
    public decimal Amount { get; set; }
    [EnumDataType(typeof(CostStage))]
    public CostStage Stage { get; set; }
    public DateOnly? PaidOn { get; set; }
}

public class ImportBuildRequest : CreateKeyboardBuildRequest
{
    public Guid Id { get; set; }
    [Required, MaxLength(500)]
    public List<ImportComponentRequest> Components { get; set; } = [];
    [Required, MaxLength(20)]
    public List<MediaRequest> Media { get; set; } = [];
    public OwnerCostsRequest? Costs { get; set; }
}

public class ImportComponentRequest : ComponentRequest
{
    public Guid Id { get; set; }
}

public class ImportSwitchRequest : CustomSwitchRequest
{
    public Guid Id { get; set; }
}

public class ImportRequest
{
    [Range(1, 1)]
    public int Version { get; set; } = 1;
    [Required, MaxLength(500)]
    public List<ImportBuildRequest> Builds { get; set; } = [];
    [Required, MaxLength(500)]
    public List<ImportSwitchRequest> CustomSwitches { get; set; } = [];
    [Required, MaxLength(500)]
    public List<ArchivedCostsRequest> ArchivedCosts { get; set; } = [];
}

public class ArchivedCostsRequest : OwnerCostsRequest
{
    public Guid KeyboardBuildId { get; set; }
}

public class MediaRequest : IValidatableObject
{
    [EnumDataType(typeof(MediaKind))]
    public MediaKind Kind { get; set; }
    [Required, StringLength(2000)]
    public string Url { get; set; } = string.Empty;
    [StringLength(200)]
    public string? Caption { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            yield return new ValidationResult("Media must use an HTTPS URL without embedded credentials.", [nameof(Url)]);
    }
}
