using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using UnifiedVanillaSave.Core;

namespace SaveAudit;

public static class PathMethodAudit
{
    public static Dictionary<string, string> Read(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        var decoder = new TypeNames();
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var th in md.TypeDefinitions)
        {
            var type = md.GetTypeDefinition(th);
            if (md.GetString(type.Namespace) + "." + md.GetString(type.Name) != Compatibility.TargetType) continue;
            foreach (var mh in type.GetMethods())
            {
                var method = md.GetMethodDefinition(mh);
                var name = md.GetString(method.Name);
                if (!Compatibility.PathMethodNames.Contains(name)) continue;
                var signature = method.DecodeSignature(decoder, (object?)null);
                string[] expected = name == "GetAccountDir"
                    ? ["System.Nullable`1<System.Boolean>"]
                    : ["System.Int32", "System.Nullable`1<System.Boolean>"];
                if ((method.Attributes & MethodAttributes.Static) == 0 || signature.Header.IsInstance ||
                    signature.GenericParameterCount != 0 || signature.ReturnType != "System.String" ||
                    !signature.ParameterTypes.SequenceEqual(expected)) continue;
                if (method.RelativeVirtualAddress == 0) throw new InvalidOperationException("No IL: " + name);
                if (!hashes.TryAdd(name, Compatibility.Hash(pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!)))
                    throw new InvalidOperationException("Ambiguous relative selector: " + name);
            }
        }
        if (Compatibility.PathMethodNames.Any(n => !hashes.ContainsKey(n)))
            throw new MissingMethodException("Expected exact relative selector signatures from the supplied v0.113.1 source. Config unchanged.");
        return hashes;
    }

    public static string UpdateConfig(string assemblyPath, string configPath)
    {
        var config = Compatibility.ReadConfig(configPath);
        if (!Compatibility.HashFile(assemblyPath).Equals(config.GameAssemblySha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("sts2.dll differs from existing audit. Do not carry an old audit across game updates.");
        var updated = config with { PathMethodIlSha256 = Read(assemblyPath) };
        var path = Path.GetFullPath(configPath);
        var backup = path + ".before-relative-paths-" + Guid.NewGuid().ToString("N") + ".bak";
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.Copy(path, backup, overwrite: false);
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return backup;
    }

    private sealed class TypeNames : ISignatureTypeProvider<string, object?>
    {
        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";
        public string GetByReferenceType(string elementType) => elementType + "&";
        public string GetFunctionPointerType(MethodSignature<string> signature) => "<function-pointer>";
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType + "<" + string.Join(",", typeArguments) + ">";
        public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;
        public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType + "<modifier:" + modifier + ">";
        public string GetPinnedType(string elementType) => elementType + " pinned";
        public string GetPointerType(string elementType) => elementType + "*";
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => "System." + typeCode;
        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var type = reader.GetTypeDefinition(handle);
            return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
        }
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var type = reader.GetTypeReference(handle);
            return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
        }
        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }
}
