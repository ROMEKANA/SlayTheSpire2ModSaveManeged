using System.Reflection;
using UnifiedVanillaSave.Core;

if (args.Length == 2 && args[0] is "inspect" or "release-audit")
{
    try
    {
        var mvid = SaveRouteAudit.Validate(args[1]);
        var version = AssemblyName.GetAssemblyName(args[1]).Version;
        Console.WriteLine($"Save routing metadata passed: assembly={version}; MVID={mvid}; SHA256={Compatibility.HashFile(args[1])}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("Save routing check failed: " + ex.Message);
        Environment.ExitCode = 1;
    }
    return;
}
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
Console.Error.WriteLine("Usage: SaveAudit release-audit <sts2.dll> | setup-config <sts2.dll> <user-root> <config.json> <fallback-backup> | paths <sts2.dll> | inspect <sts2.dll> | backup <Godot-user-data-root> <backup-directory>");
Environment.ExitCode = 2;
