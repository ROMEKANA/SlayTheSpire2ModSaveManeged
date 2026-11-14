using System.Text.Json;

namespace UnifiedVanillaSave.Core;

public sealed record BackupReceipt(string SourceRoot, Dictionary<string, string> Files);

public static class FirstBackup
{
    private const string ReceiptName = "receipt.json";
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Canonical(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public static void Ensure(string sourceRoot, string backupDirectory)
    {
        var source = Canonical(sourceRoot);
        var backup = Canonical(backupDirectory);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        if (Inside(backup, source) || Inside(source, backup))
            throw new IOException("Save and backup directories must be disjoint.");
        RejectLinkedAncestors(source);
        RejectLinkedAncestors(backup);
        // Single writer across processes; a competing game/tool fails rather than skipping protection.
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        using var guard = new FileStream(backup + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (Directory.Exists(backup))
        {
            Verify(source, backup);
            return;
        }
        var staging = backup + ".partial-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.Combine(staging, "files"));
        try
        {
            var before = Inventory(source);
            foreach (var (relative, hash) in before)
            {
                var destination = Path.Combine(staging, "files", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(source, relative), destination, overwrite: false);
                if (Compatibility.HashFile(destination) != hash)
                    throw new IOException("Source changed during backup: " + relative);
            }
            var after = Inventory(source);
            if (before.Count != after.Count || before.Any(p => !after.TryGetValue(p.Key, out var h) || h != p.Value))
                throw new IOException("Save tree changed during backup. Close the game/Cloud sync and retry.");
            File.WriteAllText(Path.Combine(staging, ReceiptName), JsonSerializer.Serialize(new BackupReceipt(source, before)));
            Directory.Move(staging, backup); // Receipt only becomes usable after the full snapshot succeeds.
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    public static void Verify(string sourceRoot, string backupDirectory)
    {
        var source = Canonical(sourceRoot);
        var backup = Canonical(backupDirectory);
        RejectLinkedAncestors(backup);
        var receiptPath = Path.Combine(backup, ReceiptName);
        RejectLink(receiptPath);
        var receipt = JsonSerializer.Deserialize<BackupReceipt>(File.ReadAllText(receiptPath))
            ?? throw new InvalidDataException("Missing backup receipt.");
        if (!string.Equals(source, receipt.SourceRoot, PathComparison))
            throw new IOException("Backup belongs to another user-data root.");
        foreach (var (relative, hash) in receipt.Files)
        {
            var file = Canonical(Path.Combine(backup, "files", relative));
            var filesRoot = Canonical(Path.Combine(backup, "files"));
            if (!Inside(file, filesRoot) || file == filesRoot) throw new IOException("Invalid backup entry.");
            RejectLinkedAncestors(file);
            if (Compatibility.HashFile(file) != hash) throw new IOException("Backup damaged: " + relative);
        }
    }

    private static bool Inside(string child, string parent) =>
        string.Equals(child, parent, PathComparison) || child.StartsWith(parent + Path.DirectorySeparatorChar, PathComparison);

    private static Dictionary<string, string> Inventory(string root)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        Visit(root);
        return files;
        void Visit(string dir)
        {
            RejectLink(dir);
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
            {
                RejectLink(entry);
                if (Directory.Exists(entry)) Visit(entry);
                else files.Add(Path.GetRelativePath(root, entry), Compatibility.HashFile(entry));
            }
        }
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Links/reparse points are not supported: " + path);
    }

    private static void RejectLinkedAncestors(string path)
    {
        for (var p = path; p is not null; p = Path.GetDirectoryName(p))
            if (Directory.Exists(p) || File.Exists(p)) RejectLink(p);
    }
}
