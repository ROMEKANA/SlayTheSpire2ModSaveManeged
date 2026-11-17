using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using UnifiedVanillaSave.Core;

if (args.Length == 5 && args[0] == "setup-config")
{
    try { SaveAudit.SetupConfig.Run(args[1], args[2], args[3], args[4]); }
    catch (Exception ex)
    {
        Console.Error.WriteLine("Automatic setup stopped: " + ex.Message);
        Environment.ExitCode = 1;
    }
    return;
}
if (args.Length == 3 && args[0] == "path-config")
{
    var backup = SaveAudit.PathMethodAudit.UpdateConfig(args[1], args[2]);
    Console.WriteLine("Relative selector IL hashes recorded. Other audit flags and save paths retained.");
    Console.WriteLine("Previous config preserved: " + backup);
    return;
}
if (args.Length == 2 && args[0] == "paths")
{
    SaveAudit.PathReport.Write(args[1], Console.Out);
    return;
}
if (args.Length == 3 && args[0] == "backup")
{
    FirstBackup.Ensure(args[1], args[2]);
    Console.WriteLine("First backup created/verified: " + Path.GetFullPath(args[2]));
    return;
}
if (args.Length != 2 || args[0] != "inspect")
{
    Console.Error.WriteLine("Usage: SaveAudit setup-config <sts2.dll> <user-root> <config.json> <fallback-backup> | path-config <sts2.dll> <config.json> | paths <sts2.dll> | inspect <sts2.dll> | backup <Godot-user-data-root> <backup-directory>");
    Environment.ExitCode = 2;
    return;
}
using var stream = File.OpenRead(args[1]);
using var pe = new PEReader(stream);
var md = pe.GetMetadataReader();
Console.WriteLine("Assembly SHA256: " + Compatibility.HashFile(args[1]));
Console.WriteLine("Assembly version: " + md.GetAssemblyDefinition().Version);
Console.WriteLine("The following is metadata evidence, NOT confirmation of vanilla layout/callers.");
foreach (var (name, hash) in SaveAudit.PathMethodAudit.Read(args[1]))
    Console.WriteLine($"Relative selector {name} IL SHA256: {hash}");
Console.WriteLine("Targets: GetAccountDir(bool?) and GetProfileDir(int,bool?). Default requests use vanilla; explicit probes retain their semantics.");
