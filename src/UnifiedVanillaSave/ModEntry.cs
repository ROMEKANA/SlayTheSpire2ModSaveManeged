using System.Reflection;
using ProjectSettings = Godot.ProjectSettings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using UnifiedVanillaSave.Core;

namespace UnifiedVanillaSave;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    private const string Owner = "ROMEKANA.UnifiedVanillaSave";
    private static readonly object Gate = new();
    private static MethodInfo[]? targets;
    private static string? logPath;
    private static readonly HashSet<string> LoggedProfiles = [];

    public static void Initialize()
    {
        lock (Gate)
        {
            if (targets is not null) return;
            var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnifiedVanillaSave");
            Directory.CreateDirectory(home);
            logPath = Path.Combine(home, "UnifiedVanillaSave.log");
            var harmony = new Harmony(Owner);
            try
            {
                var configPath = Environment.GetEnvironmentVariable("UVS_CONFIG") ?? Path.Combine(home, "config.json");
                var config = Compatibility.ReadConfig(configPath);
                var game = typeof(ModInitializerAttribute).Assembly;
                Log($"Loaded mod: {typeof(ModEntry).Assembly.Location}; SHA256={Compatibility.HashFile(typeof(ModEntry).Assembly.Location)}");
                Log($"Game assembly: {game.Location}; MVID={game.ManifestModule.ModuleVersionId}");
                var methods = Compatibility.ResolvePaths(game, config);
                Log($"Audit accepted: game={config.AuditedGameVersion}, sha256={config.GameAssemblySha256}");
                foreach (var method in methods) Log("Audited final path builder: " + method);
                var actualRoot = FirstBackup.Canonical(ProjectSettings.GlobalizePath("user://"));
                if (!string.Equals(actualRoot, FirstBackup.Canonical(config.UserDataRoot),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    throw new InvalidOperationException("Configured user-data root differs from Godot user://.");
                CheckConflicts(methods);
                FirstBackup.Ensure(actualRoot, config.BackupDirectory);
                Log("First backup verified: " + config.BackupDirectory);
                targets = methods;
                foreach (var method in methods)
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(ModEntry), nameof(PathPostfix)) { priority = Priority.Last });
                CheckConflicts(methods);
                Log("PATCH_INSTALLED: final profile path/base builders patched. Global mod state and explicit GetProfileDir probes unchanged. Actual file I/O still requires verification.");
            }
            catch (Exception ex)
            {
                if (targets is not null)
                    foreach (var method in targets) harmony.Unpatch(method, HarmonyPatchType.Postfix, Owner);
                targets = null;
                Log("INACTIVE: no save unification. Exit the game before playing. " + ex);
                throw;
            }
        }
    }

    private static void PathPostfix(int __0, MethodBase __originalMethod, ref string __result)
    {
        CheckConflicts(targets ?? throw new InvalidOperationException("Patches not initialized."));
        var original = __result;
        __result = ProfilePaths.Vanilla(original, __0);
        lock (Gate)
        {
            var key = __originalMethod.Name + ":" + __0;
            if (LoggedProfiles.Count < 32 && LoggedProfiles.Add(key))
                Log($"PROFILE_ROUTE: {__originalMethod.Name}; original='{original}'; vanilla='{__result}'. Final file I/O is not observed by this log.");
        }
    }

    private static void CheckConflicts(MethodInfo[] methods)
    {
        var type = methods[0].DeclaringType!;
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        // These methods can affect final path construction; detect other owners without patching them.
        var related = type.GetMethods(flags).Where(m =>
            Compatibility.PathMethodNames.Contains(m.Name) || m.Name is
                "GetProfileDir" or "GetAccountDir" or "get_IsRunningModded" or "set_IsRunningModded");
        var foreign = related.SelectMany(m => Harmony.GetPatchInfo(m)?.Owners ?? Enumerable.Empty<string>())
            .Where(o => o != Owner).Distinct().ToArray();
        if (foreign.Length == 0) return;
        var message = "Conflicting Harmony owners on save path provider: " + string.Join(", ", foreign);
        Log(message);
        throw new InvalidOperationException(message);
    }

    private static void Log(string message)
    {
        var line = $"[{DateTimeOffset.UtcNow:O}] [UnifiedVanillaSave] {message}";
        Console.Error.WriteLine(line);
        if (logPath is not null) File.AppendAllText(logPath, line + Environment.NewLine);
    }
}
