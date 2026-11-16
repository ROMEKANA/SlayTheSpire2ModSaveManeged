using System.Globalization;
using UnifiedVanillaSave.Core;

var tests = new (string Name, Action Run)[]
{
    ("profile path uses invariant vanilla format", () =>
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Equal("profile0", Compatibility.VanillaProfile(0));
            Equal("profile2147483647", Compatibility.VanillaProfile(int.MaxValue));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }),
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
        Throws<InvalidOperationException>(() => Compatibility.Resolve(typeof(Compatibility).Assembly,
            new AuditConfig("", "", "", "", "", false)))),
    ("exact method and IL audit accepted on a test fixture only", () =>
    {
        var game = typeof(MegaCrit.Sts2.Core.Saves.UserDataPathProvider).Assembly;
        var method = typeof(MegaCrit.Sts2.Core.Saves.UserDataPathProvider).GetMethod("GetProfileDir", [typeof(int)])!;
        var config = new AuditConfig(Compatibility.HashFile(game.Location),
            Compatibility.Hash(method.GetMethodBody()!.GetILAsByteArray()!), "", "", "fixture", true);
        if (Compatibility.Resolve(game, config) != method) throw new Exception("Wrong method");
        Throws<InvalidOperationException>(() => Compatibility.Resolve(game, config with { MethodIlSha256 = "bad" }));
    }),
    ("path report resolves both overloads and their direct callers without loading game code", () =>
    {
        using var report = new StringWriter();
        SaveAudit.PathReport.Write(typeof(UnifiedVanillaSave.Tests.PathCallerFixture).Assembly.Location, report);
        var text = report.ToString();
        foreach (var expected in new[] { "PathCallerFixture.ViaOne", "PathCallerFixture.ViaTwo",
            "GetProfileDir [signature=00010E08]", "ldstr", "test-fixture-", "Direct references from outside" })
            if (!text.Contains(expected, StringComparison.Ordinal)) throw new Exception("Missing report evidence: " + expected);
    }),
    ("path report rejects an assembly without the provider", () =>
        Throws<MissingMemberException>(() => SaveAudit.PathReport.Write(typeof(Compatibility).Assembly.Location, TextWriter.Null))),
    ("assembly change rejected before binding a target", () =>
        Throws<InvalidOperationException>(() => Compatibility.Resolve(typeof(Compatibility).Assembly,
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
