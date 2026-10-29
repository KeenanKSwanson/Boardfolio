namespace Boardfolio.Api.Models
{
    public class KeyboardBuild
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Assigned from the signed-in account when saved on the server.
        [System.Text.Json.Serialization.JsonIgnore]
        public Guid OwnerUserId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string KeyboardModel { get; set; } = string.Empty;

        public string? Notes {  get; set; }

        public List<KeyboardBuildComponent> Components { get; set; } = [];
        public List<BuildMedia> Media { get; set; } = [];
    }

}

