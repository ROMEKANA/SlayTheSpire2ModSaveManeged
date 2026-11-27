using UnifiedVanillaSave.Core;

namespace SaveAudit;

public static class SetupConfig
{
    public static void Run(string assemblyPath, string userRoot, string configPath, string fallbackBackup)
    {
        SaveRouteAudit.Validate(assemblyPath);
        var root = FirstBackup.Canonical(userRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var path = Path.GetFullPath(configPath);
        var existing = File.Exists(path) ? SaveSettings.Read(path) : null;
        var settings = new SaveSettings(root, existing?.BackupDirectory ?? FirstBackup.Canonical(fallbackBackup));
        FirstBackup.Ensure(root, settings.BackupDirectory);
        if (existing == settings)
        {
            Console.WriteLine("Config already current; first backup verified.");
            return;
        }
        if (existing is not null)
        {
            var backup = path + ".before-setup-" + Guid.NewGuid().ToString("N") + ".bak";
            File.Copy(path, backup, overwrite: false);
            Console.WriteLine("Previous config preserved: " + backup);
        }
        SaveSettings.Write(path, settings, overwrite: existing is not null);
        Console.WriteLine("Config generated automatically; first backup verified: " + settings.BackupDirectory);
    }
}
