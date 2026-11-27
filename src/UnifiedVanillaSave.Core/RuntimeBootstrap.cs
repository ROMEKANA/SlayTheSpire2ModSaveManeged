using System.Text.Json;

namespace UnifiedVanillaSave.Core;

public sealed record SaveSettings(string UserDataRoot, string BackupDirectory)
{
    public static SaveSettings Read(string path)
    {
        // Legacy files also contain version/hash audits. Only the two location settings still apply.
        var settings = JsonSerializer.Deserialize<SaveSettings>(File.ReadAllText(path), new JsonSerializerOptions
            { PropertyNameCaseInsensitive = true });
        if (string.IsNullOrWhiteSpace(settings?.UserDataRoot) || string.IsNullOrWhiteSpace(settings.BackupDirectory))
            throw new InvalidDataException("Config must contain UserDataRoot and BackupDirectory.");
        return settings;
    }

    public static void Write(string path, SaveSettings settings, bool overwrite)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public static class RuntimeBootstrap
{
    // Called only after save routing has been validated.
    public static SaveSettings LoadOrCreate(string root, string configPath, string backup)
    {
        var path = Path.GetFullPath(configPath);
        if (File.Exists(path)) return SaveSettings.Read(path);
        var settings = new SaveSettings(FirstBackup.Canonical(root), FirstBackup.Canonical(backup));
        try { SaveSettings.Write(path, settings, overwrite: false); }
        catch (IOException) when (File.Exists(path)) { return SaveSettings.Read(path); }
        return settings;
    }
}
