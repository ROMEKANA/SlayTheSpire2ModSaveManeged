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
                var config = Compatibility.ReadConfig(configPath);
                var game = typeof(ModInitializerAttribute).Assembly;
                Log($"Loaded mod: {typeof(ModEntry).Assembly.Location}; SHA256={Compatibility.HashFile(typeof(ModEntry).Assembly.Location)}");
                Log($"Game assembly: {game.Location}; MVID={game.ManifestModule.ModuleVersionId}");
                var methods = Compatibility.ResolvePaths(game, config);
                Log($"Audit accepted: game={config.AuditedGameVersion}, sha256={config.GameAssemblySha256}");
                foreach (var method in methods) Log("Audited relative selector: " + method);
                var actualRoot = FirstBackup.Canonical(ProjectSettings.GlobalizePath("user://"));
                if (!string.Equals(actualRoot, FirstBackup.Canonical(config.UserDataRoot),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    throw new InvalidOperationException("Configured user-data root differs from Godot user://.");
                SaveRoutingPatches.CheckConflicts(methods, Owner);
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
