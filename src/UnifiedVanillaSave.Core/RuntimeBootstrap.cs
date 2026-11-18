using System.Reflection;
using System.Text.Json;

namespace UnifiedVanillaSave.Core;

public static class RuntimeBootstrap
{
    public static AuditConfig NewReviewedConfig(string hash, string root, string backup, Dictionary<string, string> methodHashes)
    {
        var version = ReviewedBuilds.VersionFor(hash)
            ?? throw new InvalidOperationException("Unsupported game build. No config generated; obtain a source review for this exact DLL.");
        return new AuditConfig(hash, "", FirstBackup.Canonical(root), FirstBackup.Canonical(backup), version, true, methodHashes);
    }

    // Called before any routing hooks. Existing config is read, not overwritten.
    // The caller must still validate roots/targets and complete FirstBackup.Ensure before patching.
    public static AuditConfig LoadOrCreate(Assembly game, string root, string configPath, string backup)
    {
        var path = Path.GetFullPath(configPath);
        if (File.Exists(path)) return Compatibility.ReadConfig(path);
        var hash = Compatibility.HashFile(game.Location);
        if (ReviewedBuilds.VersionFor(hash) is null)
            throw new InvalidOperationException($"Unsupported game build SHA256={hash}; no first-run config was created.");
        var hashes = Compatibility.FindPaths(game).ToDictionary(m => m.Name, m =>
            Compatibility.Hash(m.GetMethodBody()?.GetILAsByteArray() ?? throw new InvalidOperationException("No IL: " + m.Name)));
        var config = NewReviewedConfig(hash, root, backup, hashes);
        Compatibility.ResolvePaths(game, config);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: false); // Never replace a concurrently created user config.
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return config;
    }
}
