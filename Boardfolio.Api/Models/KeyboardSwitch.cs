using System.ComponentModel.DataAnnotations;

namespace Boardfolio.Api.Models
{
    public class KeyboardSwitch
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Manufacturer { get; set; } = string.Empty;

        [EnumDataType(typeof(SwitchType))]
        public SwitchType Type { get; set; } = SwitchType.Unknown;
    }

    public enum SwitchType
    {
        Unknown = 0,
        Linear = 1,
        Tactile = 2,
        Clicky = 3
    }
}
