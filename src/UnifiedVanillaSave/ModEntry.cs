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
    private static bool installed;
    private static string? logPath;

    public static void Initialize()
    {
        lock (Gate)
        {
            if (installed) return;
            var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnifiedVanillaSave");
            Directory.CreateDirectory(home);
            logPath = Path.Combine(home, "UnifiedVanillaSave.log");
            var harmony = new Harmony(Owner);
            try
            {
                var configPath = Environment.GetEnvironmentVariable("UVS_CONFIG") ?? Path.Combine(home, "config.json");
                var game = typeof(ModInitializerAttribute).Assembly;
                Log($"Loaded mod: {typeof(ModEntry).Assembly.Location}; SHA256={Compatibility.HashFile(typeof(ModEntry).Assembly.Location)}");
                Log($"Game assembly: {game.Location}; MVID={game.ManifestModule.ModuleVersionId}; SHA256={Compatibility.HashFile(game.Location)}");
                var methods = Compatibility.ResolvePaths(game);
                SaveRoutingPatches.CheckConflicts(methods, Owner);
                Compatibility.CheckSelectorBehavior(methods);
                Log("Save routing compatibility checked: path factories and vanilla/modded selectors.");
                var actualRoot = FirstBackup.Canonical(ProjectSettings.GlobalizePath("user://"));
                var hadConfig = File.Exists(configPath);
                var config = RuntimeBootstrap.LoadOrCreate(actualRoot, configPath,
                    Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configPath))!, "backups", "first"));
                Log($"Config {(hadConfig ? "loaded" : "created automatically")}: {configPath}");
                if (!string.Equals(actualRoot, FirstBackup.Canonical(config.UserDataRoot),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    throw new InvalidOperationException("Configured user-data root differs from Godot user://.");
                FirstBackup.Ensure(actualRoot, config.BackupDirectory);
                Log("First backup verified: " + config.BackupDirectory);
                SaveRoutingPatches.Install(harmony, methods, Owner, Log);
                installed = true;
                Log("PATCH_INSTALLED: GetAccountDir(bool?) and GetProfileDir(int,bool?) route default requests to vanilla. Global mod state and explicit probes unchanged.");
            }
            catch (Exception ex)
            {
                SaveRoutingPatches.Remove(harmony);
                installed = false;
                Log("INACTIVE: no save unification. Exit the game before playing. " + ex);
                throw;
            }
        }
    }

    private static void Log(string message)
    {
        var line = $"[{DateTimeOffset.UtcNow:O}] [UnifiedVanillaSave] {message}";
        lock (Gate)
        {
            Console.Error.WriteLine(line);
            if (logPath is not null) File.AppendAllText(logPath, line + Environment.NewLine);
        }
    }
}
