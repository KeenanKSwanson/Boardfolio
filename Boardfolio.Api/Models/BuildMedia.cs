namespace Boardfolio.Api.Models;

// A user-supplied media link. File upload/storage is a separate mobile integration.
public class BuildMedia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid KeyboardBuildId { get; set; }
    public MediaKind Kind { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Caption { get; set; }
}

public enum MediaKind { Photo = 0, Video = 1, SoundTest = 2 }
