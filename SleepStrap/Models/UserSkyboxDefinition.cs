namespace SleepStrap.Models
{
    /// <summary>
    /// Metadata for a panorama imported by the user. The display name is never used
    /// as a file-system path; <see cref="Id"/> owns the on-disk folder instead.
    /// </summary>
    public sealed class UserSkyboxDefinition
    {
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
