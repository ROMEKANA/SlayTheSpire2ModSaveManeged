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
    private static MethodInfo? target;
    private static string? logPath;
    private static readonly HashSet<int> LoggedProfiles = [];

    public static void Initialize()
    {
        lock (Gate)
        {
            if (target is not null) return;
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
                var method = Compatibility.Resolve(game, config);
                Log($"Audit accepted: game={config.AuditedGameVersion}, sha256={config.GameAssemblySha256}, target={method}");
                var actualRoot = FirstBackup.Canonical(ProjectSettings.GlobalizePath("user://"));
                if (!string.Equals(actualRoot, FirstBackup.Canonical(config.UserDataRoot),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    throw new InvalidOperationException("Configured user-data root differs from Godot user://.");
                CheckConflicts(method);
                FirstBackup.Ensure(actualRoot, config.BackupDirectory);
                Log("First backup verified: " + config.BackupDirectory);
                target = method;
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(ModEntry), nameof(ProfilePrefix)) { priority = Priority.First });
                CheckConflicts(method);
                Log("PATCH_INSTALLED: only GetProfileDir(int) patched. Actual save routing is NOT verified. Look for PROFILE_ROUTE and inspect saved files.");
            }
            catch (Exception ex)
            {
                if (target is not null)
                    harmony.Unpatch(target, HarmonyPatchType.Prefix, Owner);
                target = null;
                Log("INACTIVE: no save unification. Exit the game before playing. " + ex);
                throw;
            }
        }
    }

    private static bool ProfilePrefix(int __0, ref string __result)
    {
        // A later mod may have patched the same target. Never silently continue with mixed destinations.
        CheckConflicts(target ?? throw new InvalidOperationException("Patch not initialized."));
        __result = Compatibility.VanillaProfile(__0);
        lock (Gate)
        {
            if (LoggedProfiles.Count < 16 && LoggedProfiles.Add(__0))
                Log($"PROFILE_ROUTE: GetProfileDir(int) invoked; profileId={__0}; relative result='{__result}'. This does not confirm the final read/write path.");
        }
        return false;
    }

    private static void CheckConflicts(MethodBase method)
    {
        var type = method.DeclaringType!;
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        // Detect legacy unifiers that patch global state, without patching that state ourselves.
        var related = new MethodBase?[] { method,
            type.GetMethod("get_IsRunningModded", flags), type.GetMethod("set_IsRunningModded", flags) };
        var foreign = related.Where(m => m is not null)
            .SelectMany(m => Harmony.GetPatchInfo(m!)?.Owners ?? Enumerable.Empty<string>())
            .Where(o => o != Owner).Distinct().ToArray();
        if (foreign.Length == 0) return;
        var message = "Conflicting Harmony owners on GetProfileDir: " + string.Join(", ", foreign);
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
