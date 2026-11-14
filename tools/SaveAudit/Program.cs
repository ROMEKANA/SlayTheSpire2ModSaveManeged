using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using UnifiedVanillaSave.Core;

if (args.Length == 3 && args[0] == "backup")
{
    FirstBackup.Ensure(args[1], args[2]);
    Console.WriteLine("First backup created/verified: " + Path.GetFullPath(args[2]));
    return;
}
if (args.Length != 2 || args[0] != "inspect")
{
    Console.Error.WriteLine("Usage: SaveAudit inspect <sts2.dll> | backup <Godot-user-data-root> <backup-directory>");
    Environment.ExitCode = 2;
    return;
}
using var stream = File.OpenRead(args[1]);
using var pe = new PEReader(stream);
var md = pe.GetMetadataReader();
Console.WriteLine("Assembly SHA256: " + Compatibility.HashFile(args[1]));
Console.WriteLine("Assembly version: " + md.GetAssemblyDefinition().Version);
Console.WriteLine("The following is metadata evidence, NOT confirmation of vanilla layout/callers.");
var matchingCandidates = 0;
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
        Console.WriteLine("Expected static string(int32) signature blob: 00010E08");
        if ((method.Attributes & System.Reflection.MethodAttributes.Static) != 0 &&
            Convert.ToHexString(md.GetBlobBytes(method.Signature)) == "00010E08") matchingCandidates++;
        if (method.RelativeVirtualAddress != 0)
        {
            var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
            Console.WriteLine("Method IL SHA256: " + Compatibility.Hash(il));
            Console.WriteLine("IL bytes: " + Convert.ToHexString(il));
        }
    }
}

if (matchingCandidates != 1)
{
    Console.Error.WriteLine($"Expected exactly one static string(int32) candidate; found {matchingCandidates}. Do not enable the mod.");
    Environment.ExitCode = 1;
}
