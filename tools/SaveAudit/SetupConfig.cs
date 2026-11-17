using System.Text.Json;
using UnifiedVanillaSave.Core;

namespace SaveAudit;

public static class SetupConfig
{
    // Fingerprint from the user's v0.113.1 runtime log, paired with the reviewed provider source.
    // Unknown builds still require a matching previously confirmed local audit.
    public const string ReviewedGameHash = "62ECBE91093E1FA862DE4329AAC865209DCB297804CAF0F0520D92D8C0B4A9C3";

    public static AuditConfig Choose(AuditConfig? existing, string assemblyHash, string userRoot, string fallbackBackup)
    {
        var known = assemblyHash.Equals(ReviewedGameHash, StringComparison.OrdinalIgnoreCase);
        var locallyReviewed = existing is not null && existing.CallersAndVanillaLayoutVerified &&
            !string.IsNullOrWhiteSpace(existing.AuditedGameVersion) &&
            assemblyHash.Equals(existing.GameAssemblySha256, StringComparison.OrdinalIgnoreCase);
        if (!known && !locallyReviewed)
            throw new InvalidOperationException("This game DLL has no matching source audit. Setup stopped before changing config or saves. Supply a SaveAudit paths report for review; do not hand-edit audit flags.");
        return new AuditConfig(assemblyHash, existing?.MethodIlSha256 ?? "", userRoot,
            string.IsNullOrWhiteSpace(existing?.BackupDirectory) ? fallbackBackup : existing.BackupDirectory,
            known ? "0.113.1" : existing!.AuditedGameVersion, true);
    }

    public static void Run(string assemblyPath, string userRoot, string configPath, string fallbackBackup)
    {
        var root = FirstBackup.Canonical(userRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var path = Path.GetFullPath(configPath);
        var existing = File.Exists(path) ? Compatibility.ReadConfig(path) : null;
        var config = Choose(existing, Compatibility.HashFile(assemblyPath), root, fallbackBackup)
            with { PathMethodIlSha256 = PathMethodAudit.Read(assemblyPath) };
        FirstBackup.Ensure(root, config.BackupDirectory);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        if (File.Exists(path) && File.ReadAllText(path) == json)
        {
            Console.WriteLine("Config already current; first backup verified.");
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            var backup = path + ".before-setup-" + Guid.NewGuid().ToString("N") + ".bak";
            File.Copy(path, backup, overwrite: false);
            Console.WriteLine("Previous config preserved: " + backup);
        }
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        Console.WriteLine("Config generated automatically; first backup verified: " + config.BackupDirectory);
    }
}
