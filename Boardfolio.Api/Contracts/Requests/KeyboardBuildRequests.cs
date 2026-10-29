using System.ComponentModel.DataAnnotations;

namespace Boardfolio.Api.Contracts.Requests
{
    public class CreateKeyboardBuildRequest
    {

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string KeyboardModel { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Notes { get; set; }
    }

    public class UpdateKeyboardBuildRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string KeyboardModel { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Notes { get; set; }
    }
}

