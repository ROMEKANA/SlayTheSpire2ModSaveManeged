using HarmonyLib;
using UnifiedVanillaSave;
using UnifiedVanillaSave.Core;
using Provider = MegaCrit.Sts2.Core.Saves.UserDataPathProvider;

// Independent contract/patch tests. These do not compile the full MOD or use the game's assemblies/stores.
const string owner = "test.unifiedvanillasave";
const string other = "test.other";
var harmony = new Harmony(owner);
var foreign = new Harmony(other);
var game = typeof(Provider).Assembly;
var config = new AuditConfig(Compatibility.HashFile(game.Location), "", "", "", "fixture", true,
    SaveAudit.PathMethodAudit.Read(game.Location));
var methods = Compatibility.ResolvePaths(game, config);
var count = 0;
void Check(string name, Action action) { action(); count++; Console.WriteLine("PASS " + name); }
try
{
    Provider.IsRunningModded = true;
    Check("warm manager path factories initially use the separate modded keys", () =>
    {
        Equal("modded/profile.save", Factory.Profile());
        Equal("user://steam/42/modded/profile2/saves", Provider.GetProfileScopedPath(2, "saves", MegaCrit.Sts2.Core.Platform.PlatformType.Steam, 42));
        foreach (var path in Factory.Paths(2)) if (!path.StartsWith("modded/profile2/", StringComparison.Ordinal)) throw new Exception(path);
    });
    Check("foreign selector patches reject installation without removing their owner", () =>
    {
        foreign.Patch(methods[0], prefix: new HarmonyMethod(typeof(Foreign), nameof(Foreign.Prefix)));
        Throws<InvalidOperationException>(() => SaveRoutingPatches.Install(harmony, methods, owner, _ => { }));
        if (!Harmony.GetPatchInfo(methods[0])!.Owners.Contains(other)) throw new Exception("Foreign patch removed");
        foreign.Unpatch(methods[0], HarmonyPatchType.Prefix, other);
    });
    var logs = new List<string>();
    SaveRoutingPatches.Install(harmony, methods, owner, logs.Add);
    Check("account profile selection reads vanilla last-profile state", () =>
    {
        var store = new Dictionary<string, string> { ["profile.save"] = "2", ["modded/profile.save"] = "1" };
        Equal("2", store[Factory.Profile()]);
        Equal("1", store[Factory.Profile(true)]);
    });
    Check("all warmed manager paths now select vanilla without changing mod state", () =>
    {
        foreach (var modded in new[] { false, true })
        {
            Provider.IsRunningModded = modded;
            foreach (var id in new[] { 1, 2, 3 })
            {
                Equal("profile.save", Factory.Profile());
                Equal("profile" + id, Provider.GetProfileDir(id));
                foreach (var path in Factory.Paths(id))
                    if (!path.StartsWith($"profile{id}/saves/", StringComparison.Ordinal)) throw new Exception(path);
            }
            if (Provider.IsRunningModded != modded) throw new Exception("Global mod state changed");
        }
    });
    Check("explicit vanilla and modded probes retain original path semantics", () =>
    {
        Provider.IsRunningModded = true;
        foreach (var id in new[] { 1, 2, 3 })
        {
            Equal("profile" + id, Provider.GetProfileDir(id, false));
            Equal("modded/profile" + id, Provider.GetProfileDir(id, true));
            foreach (var path in Factory.Paths(id, true))
                if (!path.StartsWith($"modded/profile{id}/saves/", StringComparison.Ordinal)) throw new Exception(path);
            foreach (var path in Factory.Paths(id, false))
                if (!path.StartsWith($"profile{id}/saves/", StringComparison.Ordinal)) throw new Exception(path);
        }
        Equal("modded/profile.save", Factory.Profile(true));
        Equal("profile.save", Factory.Profile(false));
    });
    Check("fixture vanilla-to-modded read/write round trip uses shared keys without copying old modded data", () =>
    {
        var store = new Dictionary<string, string>();
        SaveRoutingPatches.Remove(harmony);
        Provider.IsRunningModded = false;
        var vanilla = Factory.Paths(2).ToArray();
        foreach (var key in vanilla) store[key] = "vanilla progress";
        Provider.IsRunningModded = true;
        var oldModded = Factory.Paths(2).ToArray();
        foreach (var key in oldModded) store[key] = "old modded progress";
        SaveRoutingPatches.Install(harmony, methods, owner, logs.Add);
        foreach (var key in Factory.Paths(2))
        {
            Equal("vanilla progress", store[key]);
            store[key] = "new shared progress";
        }
        SaveRoutingPatches.Remove(harmony);
        Provider.IsRunningModded = false;
        foreach (var key in Factory.Paths(2)) Equal("new shared progress", store[key]);
        foreach (var key in oldModded) Equal("old modded progress", store[key]);
        SaveRoutingPatches.Install(harmony, methods, owner, logs.Add);
    });
    Check("absolute provider helpers retain platform/user IDs and arbitrary file names", () =>
    {
        Provider.IsRunningModded = true;
        Equal("user://steam/42/profile3/saves", Provider.GetProfileScopedPath(3, "saves", MegaCrit.Sts2.Core.Platform.PlatformType.Steam, 42));
        Equal("profile3/saves/custom_modded_file.backup", Factory.Run(3, "custom_modded_file.backup"));
        if (!logs.Any(s => s.Contains("RELATIVE_ROUTE:") && s.Contains("relative='profile"))) throw new Exception("No routing observations");
    });
    Check("a late conflicting owner is detected and cleanup preserves that owner", () =>
    {
        foreign.Patch(methods[0], prefix: new HarmonyMethod(typeof(Foreign), nameof(Foreign.Prefix)));
        Throws<InvalidOperationException>(() => Factory.Profile());
        SaveRoutingPatches.Remove(harmony);
        var owners = Harmony.GetPatchInfo(methods[0])!.Owners;
        if (owners.Contains(owner) || !owners.Contains(other)) throw new Exception("Incorrect cleanup");
        foreign.Unpatch(methods[0], HarmonyPatchType.Prefix, other);
        Provider.IsRunningModded = true;
        Equal("modded/profile.save", Factory.Profile());
    });
    Console.WriteLine($"{count}/{count} Harmony fixture checks passed");
}
finally
{
    SaveRoutingPatches.Remove(harmony);
    foreach (var method in methods) foreign.Unpatch(method, HarmonyPatchType.Prefix, other);
}
static void Equal(string expected, string actual)
{
    if (expected != actual) throw new Exception($"Expected '{expected}', got '{actual}'");
}
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
public static class Foreign { public static void Prefix() { } }
public static class Factory
{
    public static string Profile(bool? mode = null) => Provider.Join(Provider.GetAccountDir(mode), "profile.save");
    public static string Progress(int id, bool? mode = null) => File(id, "progress.save", mode);
    public static string Prefs(int id, bool? mode = null) => File(id, "prefs.save", mode);
    public static string History(int id, bool? mode = null) => File(id, "history", mode);
    public static string Run(int id, string name, bool? mode = null) => File(id, name, mode);
    private static string File(int id, string name, bool? mode) => Provider.Join(Provider.GetProfileDir(id, mode), "saves/" + name);
    public static IEnumerable<string> Paths(int id, bool? mode = null)
    {
        yield return Progress(id, mode); yield return Prefs(id, mode); yield return History(id, mode);
        yield return Run(id, "current_run.save", mode); yield return Run(id, "current_run_mp.save", mode);
    }
}
