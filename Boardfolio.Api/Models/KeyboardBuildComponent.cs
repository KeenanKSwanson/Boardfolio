using System.ComponentModel.DataAnnotations;

namespace Boardfolio.Api.Models;

public class KeyboardBuildComponent : IValidatableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid KeyboardBuildId { get; set; }

    [EnumDataType(typeof(ComponentType))]
    public ComponentType Type { get; set; } = ComponentType.Other;

    // Optional to allow manually described switches that are not yet in either catalog.
    public Guid? KeyboardSwitchId { get; set; }

    public Guid? CustomSwitchId { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Manufacturer { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    [StringLength(2000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (KeyboardSwitchId == Guid.Empty)
            yield return new ValidationResult("A catalog switch ID cannot be empty.", [nameof(KeyboardSwitchId)]);

        if (CustomSwitchId == Guid.Empty)
            yield return new ValidationResult("A custom switch ID cannot be empty.", [nameof(CustomSwitchId)]);

        if (KeyboardSwitchId.HasValue && CustomSwitchId.HasValue)
            yield return new ValidationResult(
                "Select either a catalog switch or a custom switch, not both.",
                [nameof(KeyboardSwitchId), nameof(CustomSwitchId)]);

        if (Type != ComponentType.Switches && (KeyboardSwitchId.HasValue || CustomSwitchId.HasValue))
            yield return new ValidationResult(
                "Only switch components can reference a switch.",
                [nameof(Type), nameof(KeyboardSwitchId), nameof(CustomSwitchId)]);
    }
}

public enum ComponentType
{
    Other = 0,
    Kit = 1,
    Case = 2,
    Pcb = 3,
    Plate = 4,
    Switches = 5,
    Keycaps = 6,
    Stabilizers = 7,
    Foam = 8,
    Cable = 9
}
