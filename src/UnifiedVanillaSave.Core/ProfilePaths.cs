using System.Globalization;

namespace UnifiedVanillaSave.Core;

public static class ProfilePaths
{
    // Layout verified against the user's decompiled v0.113.1 provider.
    // Normalize final profile paths, leaving account-scoped paths and explicit legacy probes alone.
    public static string Vanilla(string path, int profileId)
    {
        const string scheme = "user://";
        if (!path.StartsWith(scheme, StringComparison.Ordinal)) throw Unexpected(path);
        var platformEnd = path.IndexOf('/', scheme.Length);
        if (platformEnd < 0 || path[scheme.Length..platformEnd] is not ("steam" or "default" or "editor")) throw Unexpected(path);
        var accountEnd = path.IndexOf('/', platformEnd + 1);
        if (accountEnd < 0 || !ulong.TryParse(path.AsSpan(platformEnd + 1, accountEnd - platformEnd - 1),
            NumberStyles.None, CultureInfo.InvariantCulture, out _)) throw Unexpected(path);
        var start = accountEnd + 1;
        var modded = path.AsSpan(start).StartsWith("modded/", StringComparison.Ordinal);
        var profileStart = start + (modded ? "modded/".Length : 0);
        var profileEnd = path.IndexOf('/', profileStart);
        if (profileEnd < 0) profileEnd = path.Length;
        if (path[profileStart..profileEnd] != Compatibility.VanillaProfile(profileId)) throw Unexpected(path);
        return modded ? path.Remove(start, "modded/".Length) : path;
    }

    private static InvalidOperationException Unexpected(string path) =>
        new("Unexpected final profile path; no guessed rewrite performed: " + path);
}
