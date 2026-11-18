namespace UnifiedVanillaSave.Core;

public static class ReviewedBuilds
{
    // Paired with the owner's exported v0.113.1 provider and manager implementations.
    public const string V01131Hash = "62ECBE91093E1FA862DE4329AAC865209DCB297804CAF0F0520D92D8C0B4A9C3";
    public static string? VersionFor(string hash) =>
        hash.Equals(V01131Hash, StringComparison.OrdinalIgnoreCase) ? "0.113.1" : null;
}
