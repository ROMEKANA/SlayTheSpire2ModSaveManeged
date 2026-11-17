using UnifiedVanillaSave.Core;

var tests = new (string Name, Action Run)[]
{
    ("first backup preserves both trees and never synchronizes them", () => WithTree((source, backup) =>
    {
        FirstBackup.Ensure(source, backup);
        Equal("vanilla", File.ReadAllText(Path.Combine(backup, "files", "profile1", "saves", "progress.save")));
        Equal("modded", File.ReadAllText(Path.Combine(backup, "files", "modded", "profile1", "saves", "progress.save")));
        File.WriteAllText(Path.Combine(source, "profile1", "saves", "progress.save"), "new progress");
        FirstBackup.Ensure(source, backup);
        Equal("vanilla", File.ReadAllText(Path.Combine(backup, "files", "profile1", "saves", "progress.save")));
        Equal("modded", File.ReadAllText(Path.Combine(source, "modded", "profile1", "saves", "progress.save")));
    })),
    ("damaged backup blocks activation", () => WithTree((source, backup) =>
    {
        FirstBackup.Ensure(source, backup);
        File.WriteAllText(Path.Combine(backup, "files", "profile1", "saves", "progress.save"), "damaged");
        Throws<IOException>(() => FirstBackup.Ensure(source, backup));
    })),
    ("incomplete backup is never accepted", () => WithTree((source, backup) =>
    {
        Directory.CreateDirectory(backup);
        Throws<IOException>(() => FirstBackup.Ensure(source, backup));
    })),
    ("nested backup rejected before writing to saves", () => WithTree((source, _) =>
        Throws<IOException>(() => FirstBackup.Ensure(source, Path.Combine(source, "backup"))))),
    ("backup from another user root rejected", () => WithTree((source, backup) =>
    {
        FirstBackup.Ensure(source, backup);
        var other = source + "-other";
        Directory.CreateDirectory(other);
        Throws<IOException>(() => FirstBackup.Ensure(other, backup));
    })),
    ("missing source is not treated as an empty save", () => WithTree((source, backup) =>
        Throws<DirectoryNotFoundException>(() => FirstBackup.Ensure(source + "-missing", backup)))),
    ("symlink in save tree rejected without completed backup", () => WithTree((source, backup) =>
    {
        // Creating links on Windows may require Developer Mode. CI exercises this on Linux.
        if (OperatingSystem.IsWindows()) return;
        Directory.CreateSymbolicLink(Path.Combine(source, "alias"), Path.Combine(source, "profile1"));
        Throws<IOException>(() => FirstBackup.Ensure(source, backup));
        if (Directory.Exists(backup)) throw new Exception("Partial backup was published");
    })),
    ("unconfirmed audit rejected", () =>
        Throws<InvalidOperationException>(() => Compatibility.ResolvePaths(typeof(Compatibility).Assembly,
            new AuditConfig("", "", "", "", "", false)))),
    ("both relative selectors are validated before patching; old audits rejected", () =>
    {
        var game = typeof(MegaCrit.Sts2.Core.Saves.UserDataPathProvider).Assembly;
        var hashes = SaveAudit.PathMethodAudit.Read(game.Location);
        var config = new AuditConfig(Compatibility.HashFile(game.Location), "unused legacy hash", "", "", "fixture", true, hashes);
        var methods = Compatibility.ResolvePaths(game, config);
        if (methods.Length != 2 || !methods.Select(m => m.Name).SequenceEqual(Compatibility.PathMethodNames)) throw new Exception("Wrong targets");
        Throws<InvalidOperationException>(() => Compatibility.ResolvePaths(game, config with { PathMethodIlSha256 = null }));
        Throws<InvalidOperationException>(() => Compatibility.ResolvePaths(game, config with {
            PathMethodIlSha256 = new() { ["GetProfileScopedPath"] = "old", ["GetProfileScopedBasePath"] = "old" }
        }));
        foreach (var name in Compatibility.PathMethodNames)
        {
            var invalid = new Dictionary<string, string>(hashes) { [name] = "bad" };
            Throws<InvalidOperationException>(() => Compatibility.ResolvePaths(game, config with { PathMethodIlSha256 = invalid }));
        }
    }),
    ("path-config preserves old config, flags and backup paths while recording exact selector hashes", () => WithTree((source, _) =>
    {
        var game = typeof(MegaCrit.Sts2.Core.Saves.UserDataPathProvider).Assembly.Location;
        var config = new AuditConfig(Compatibility.HashFile(game), "legacy", source, "preserved-backup", "fixture", false);
        var path = Path.Combine(source, "audit-config");
        var original = System.Text.Json.JsonSerializer.Serialize(config);
        File.WriteAllText(path, original);
        var backup = SaveAudit.PathMethodAudit.UpdateConfig(game, path);
        Equal(original, File.ReadAllText(backup));
        var updated = Compatibility.ReadConfig(path);
        if (updated.CallersAndVanillaLayoutVerified || updated.PathMethodIlSha256?.Count != 2) throw new Exception("Config migration wrong");
        Equal(config.BackupDirectory, updated.BackupDirectory);
        Equal(config.UserDataRoot, updated.UserDataRoot);
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(config with { GameAssemblySha256 = "different" }));
        var before = File.ReadAllText(path);
        Throws<InvalidOperationException>(() => SaveAudit.PathMethodAudit.UpdateConfig(game, path));
        Equal(before, File.ReadAllText(path));
    })),
    ("path report resolves both overloads and their direct callers without loading game code", () =>
    {
        using var report = new StringWriter();
        SaveAudit.PathReport.Write(typeof(UnifiedVanillaSave.Tests.PathCallerFixture).Assembly.Location, report);
        var text = report.ToString();
        foreach (var expected in new[] { "PathCallerFixture.ViaOne", "PathCallerFixture.ViaTwo",
            "GetProfileDir [signature=00010E08]", "ldstr", "modded", "Direct references from outside" })
            if (!text.Contains(expected, StringComparison.Ordinal)) throw new Exception("Missing report evidence: " + expected);
    }),
    ("path report rejects an assembly without the provider", () =>
        Throws<MissingMemberException>(() => SaveAudit.PathReport.Write(typeof(Compatibility).Assembly.Location, TextWriter.Null))),
    ("automatic setup recognizes the source-reviewed build without manual flags", () =>
    {
        var config = SaveAudit.SetupConfig.Choose(null, SaveAudit.SetupConfig.ReviewedGameHash, "user-root", "backup-root");
        if (!config.CallersAndVanillaLayoutVerified) throw new Exception("Reviewed build not recognized");
        Equal("0.113.1", config.AuditedGameVersion);
        Equal("backup-root", config.BackupDirectory);
    }),
    ("automatic setup rejects an unknown game without a matching approved audit", () =>
    {
        Throws<InvalidOperationException>(() => SaveAudit.SetupConfig.Choose(null, "unknown", "user", "backup"));
        var previous = new AuditConfig("old", "", "user", "backup", "version", true);
        Throws<InvalidOperationException>(() => SaveAudit.SetupConfig.Choose(previous, "new", "user", "backup"));
        Throws<InvalidOperationException>(() => SaveAudit.SetupConfig.Choose(previous with { CallersAndVanillaLayoutVerified = false }, "old", "user", "backup"));
    }),
    ("automatic setup preserves existing backup settings and is repeatable without touching saves", () => WithTree((source, backup) =>
    {
        var assembly = typeof(MegaCrit.Sts2.Core.Saves.UserDataPathProvider).Assembly.Location;
        var configPath = source + ".config";
        var existing = new AuditConfig(Compatibility.HashFile(assembly), "legacy", source, backup, "fixture", true);
        var original = System.Text.Json.JsonSerializer.Serialize(existing);
        File.WriteAllText(configPath, original);
        try
        {
            SaveAudit.SetupConfig.Run(assembly, source, configPath, backup + "-unused");
            var config = Compatibility.ReadConfig(configPath);
            Equal(backup, config.BackupDirectory);
            if (config.PathMethodIlSha256?.Count != 2) throw new Exception("Missing target hashes");
            var current = File.ReadAllText(configPath);
            SaveAudit.SetupConfig.Run(assembly, source, configPath, backup + "-unused");
            Equal(current, File.ReadAllText(configPath));
            Equal("vanilla", File.ReadAllText(Path.Combine(source, "profile1", "saves", "progress.save")));
            Equal("modded", File.ReadAllText(Path.Combine(source, "modded", "profile1", "saves", "progress.save")));
        }
        finally { File.Delete(configPath); }
    })),
    ("assembly change rejected before binding a target", () =>
        Throws<InvalidOperationException>(() => Compatibility.ResolvePaths(typeof(Compatibility).Assembly,
            new AuditConfig(new string('0', 64), "", "", "", "test", true))))
};
var failed = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} passed");
Environment.ExitCode = failed == 0 ? 0 : 1;

static void Equal(string expected, string actual)
{
    if (expected != actual) throw new Exception($"Expected {expected}, got {actual}");
}
static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
static void WithTree(Action<string, string> action)
{
    var root = Path.Combine(Path.GetTempPath(), "uvs-tests-" + Guid.NewGuid().ToString("N"));
    var source = Path.Combine(root, "user");
    var backup = Path.Combine(root, "backup");
    foreach (var part in new[] { "profile1", "modded/profile1" })
    {
        var dir = Path.Combine(source, part, "saves");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "progress.save"), part.StartsWith("modded") ? "modded" : "vanilla");
    }
    try { action(source, backup); }
    finally { Directory.Delete(root, recursive: true); }
}
