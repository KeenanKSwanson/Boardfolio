using System.ComponentModel.DataAnnotations;

namespace Boardfolio.Api.Models;

public class CustomSwitch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [System.Text.Json.Serialization.JsonIgnore]
    public Guid OwnerUserId { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [EnumDataType(typeof(SwitchType))]
    public SwitchType Type { get; set; } = SwitchType.Unknown;

    // Free-form part descriptions support parts from different brands or outside the catalog.
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
