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
    Console.WriteLine("Final path/base IL hashes recorded. Other audit flags and save paths retained.");
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
    Console.WriteLine($"Final path builder {name} IL SHA256: {hash}");
Console.WriteLine("The wrapper overloads below are diagnostic only; they are NOT patched by 0.2.0.");
foreach (var handle in md.TypeDefinitions)
{
    var type = md.GetTypeDefinition(handle);
    var name = md.GetString(type.Namespace) + "." + md.GetString(type.Name);
    if (name != Compatibility.TargetType) continue;
    foreach (var mh in type.GetMethods())
    {
        var method = md.GetMethodDefinition(mh);
        if (md.GetString(method.Name) != Compatibility.TargetMethod) continue;
        Console.WriteLine($"{name}.{Compatibility.TargetMethod}: attributes={method.Attributes}; signature blob={Convert.ToHexString(md.GetBlobBytes(method.Signature))}");
        var isCandidate = (method.Attributes & System.Reflection.MethodAttributes.Static) != 0 &&
            Convert.ToHexString(md.GetBlobBytes(method.Signature)) == "00010E08";
        Console.WriteLine(isCandidate
            ? "LEGACY WRAPPER: static string GetProfileDir(int32). Not patched by 0.2.0."
            : "OTHER OVERLOAD: not patched by this version; audit direct callers before enabling.");
        if (method.RelativeVirtualAddress != 0)
        {
            var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
            Console.WriteLine((isCandidate ? "Legacy wrapper IL SHA256: " : "Other overload IL SHA256 (do not use in config): ") + Compatibility.Hash(il));
            Console.WriteLine("IL bytes: " + Convert.ToHexString(il));
        }
    }
}
