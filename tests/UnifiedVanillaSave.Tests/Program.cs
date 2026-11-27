using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using UnifiedVanillaSave.Core;
using Provider = MegaCrit.Sts2.Core.Saves.UserDataPathProvider;

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
    ("first backup skips locked Godot and Sentry diagnostics but preserves nested data", () => WithTree((source, backup) =>
    {
        var logs = Directory.CreateDirectory(Path.Combine(source, "logs"));
        var nested = Directory.CreateDirectory(Path.Combine(source, "profile1", "logs"));
        File.WriteAllText(Path.Combine(nested.FullName, "keep"), "profile data");
        using var locked = File.Open(Path.Combine(logs.FullName, "godot.log"), FileMode.Create, FileAccess.Write, FileShare.None);
        var sentry = Directory.CreateDirectory(Path.Combine(source, "sentry"));
        using var sentryLock = File.Open(Path.Combine(sentry.FullName, "session.run.lock"), FileMode.Create, FileAccess.Write, FileShare.None);
        FirstBackup.Ensure(source, backup);
        if (Directory.Exists(Path.Combine(backup, "files", "sentry"))) throw new Exception("Sentry diagnostics were backed up");
        if (Directory.Exists(Path.Combine(backup, "files", "logs"))) throw new Exception("Runtime logs were backed up");
        Equal("profile data", File.ReadAllText(Path.Combine(backup, "files", "profile1", "logs", "keep")));
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
    ("both selectors bind and preserve default and explicit save namespaces", () =>
    {
        var methods = Compatibility.ResolvePaths(typeof(Provider).Assembly);
        if (!methods.Select(m => m.Name).SequenceEqual(Compatibility.PathMethodNames)) throw new Exception("Wrong targets");
        foreach (var modded in new[] { false, true })
        {
            Provider.IsRunningModded = modded;
            Compatibility.CheckSelectorBehavior(methods);
            if (Provider.IsRunningModded != modded) throw new Exception("Global mod state changed");
        }
        Provider.ModdedDirectory = "renamed";
        try { Throws<InvalidOperationException>(() => Compatibility.CheckSelectorBehavior(methods)); }
        finally { Provider.ModdedDirectory = "modded"; }
    }),
    ("setup rejects a factory that bypasses the selectors, even when the selectors still exist", () => WithTree((source, backup) =>
    {
        var changedDll = Path.Combine(Path.GetDirectoryName(source)!, "changed.dll");
        File.WriteAllBytes(changedDll, WithoutProgressRoute(typeof(Provider).Assembly.Location));
        var config = changedDll + ".json";
        Throws<InvalidOperationException>(() => SaveAudit.SetupConfig.Run(changedDll, source, config, backup));
        if (File.Exists(config) || Directory.Exists(backup)) throw new Exception("Incompatible build wrote setup files");
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
    ("setup accepts a changed DLL hash and preserves legacy settings and first backup", () => WithTree((source, backup) =>
    {
        var assembly = typeof(Provider).Assembly.Location;
        var config = Path.Combine(Path.GetDirectoryName(source)!, "config.json");
        var original = System.Text.Json.JsonSerializer.Serialize(new
        {
            GameAssemblySha256 = "old-game",
            UserDataRoot = source,
            BackupDirectory = backup,
            CallersAndVanillaLayoutVerified = false
        });
        File.WriteAllText(config, original);
        SaveAudit.SetupConfig.Run(assembly, source, config, backup + "-unused");
        Equal(original, File.ReadAllText(config));
        Equal(backup, SaveSettings.Read(config).BackupDirectory);
        var save = Path.Combine(source, "profile1", "saves", "progress.save");
        File.WriteAllText(save, "new progress");
        SaveAudit.SetupConfig.Run(assembly, source, config, backup + "-unused");
        Equal("vanilla", File.ReadAllText(Path.Combine(backup, "files", "profile1", "saves", "progress.save")));
        Equal("new progress", File.ReadAllText(save));
    })),
    ("fresh setup and runtime create location-only config, and runtime preserves legacy JSON", () => WithTree((source, backup) =>
    {
        var path = Path.Combine(Path.GetDirectoryName(source)!, "runtime.json");
        var settings = RuntimeBootstrap.LoadOrCreate(source, path, backup);
        Equal(source, settings.UserDataRoot);
        Equal(backup, settings.BackupDirectory);
        var json = File.ReadAllText(path);
        if (json.Contains("GameAssemblySha256") || json.Contains("AuditedGameVersion")) throw new Exception("Build pin in settings");
        var legacy = System.Text.Json.JsonSerializer.Serialize(new
        {
            settings.UserDataRoot,
            settings.BackupDirectory,
            GameAssemblySha256 = "older-dll"
        });
        File.WriteAllText(path, legacy);
        Equal(backup, RuntimeBootstrap.LoadOrCreate(source, path, backup + "-unused").BackupDirectory);
        Equal(legacy, File.ReadAllText(path));
        var setupPath = Path.Combine(Path.GetDirectoryName(source)!, "setup.json");
        SaveAudit.SetupConfig.Run(typeof(Provider).Assembly.Location, source, setupPath, backup);
        Equal(json, File.ReadAllText(setupPath));
    })),
    ("an assembly without the save provider is rejected", () =>
        Throws<TypeLoadException>(() => Compatibility.ResolvePaths(typeof(Compatibility).Assembly)))
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
static byte[] WithoutProgressRoute(string assembly)
{
    var bytes = File.ReadAllBytes(assembly);
    using var pe = new PEReader(new MemoryStream(bytes));
    var md = pe.GetMetadataReader();
    var method = md.MethodDefinitions.Select(md.GetMethodDefinition)
        .Single(m => md.StringComparer.Equals(m.Name, "GetProgressPathForProfile"));
    var rva = method.RelativeVirtualAddress;
    var body = pe.GetMethodBody(rva);
    var call = IlReader.Read(body.GetILBytes()!).First(i => i.Code == OpCodes.Call);
    var section = pe.PEHeaders.SectionHeaders[pe.PEHeaders.GetContainingSectionIndex(rva)];
    var start = rva - section.VirtualAddress + section.PointerToRawData;
    var headerSize = (bytes[start] & 3) == 2 ? 1 : (bytes[start + 1] >> 4) * 4;
    // Preserve the operand width for metadata inspection. This fixture is never executed.
    bytes[start + headerSize + call.Offset] = unchecked((byte)OpCodes.Ldc_I4.Value);
    return bytes;
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
