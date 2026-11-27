using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace UnifiedVanillaSave.Core;

// Shared by setup, Workshop preparation and runtime. Reads metadata and IL without loading code.
public static class SaveRouteAudit
{
    private const string Saves = "MegaCrit.Sts2.Core.Saves.";
    private const string NullableBool = "System.Nullable`1<System.Boolean>";
    private static readonly TypeNames Decoder = new();

    public static Guid Validate(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        // Locate the known path factories once, including the account-level one for profile.save.
        var types = md.TypeDefinitions.ToDictionary(handle =>
        {
            var type = md.GetTypeDefinition(handle);
            return (Namespace: md.GetString(type.Namespace), Name: md.GetString(type.Name), Parent: type.GetDeclaringType());
        });
        TypeDefinition FindType(string ns, string name)
        {
            if (!types.TryGetValue((ns, name, default), out var handle))
                throw new TypeLoadException($"Save routing type is missing: {ns}.{name}");
            return md.GetTypeDefinition(handle);
        }

        var provider = FindType(Saves.TrimEnd('.'), "UserDataPathProvider");
        var account = FindSelector(md, provider, "GetAccountDir", [NullableBool]);
        var profile = FindSelector(md, provider, "GetProfileDir", ["System.Int32", NullableBool]);
        var profileTargets = new HashSet<int> { MetadataTokens.GetToken(profile) };
        foreach (var wrapper in provider.GetMethods())
        {
            if (Matches(md, wrapper, "GetProfileDir", ["System.Int32"]) && Calls(pe, md, wrapper, profileTargets))
                profileTargets.Add(MetadataTokens.GetToken(wrapper));
        }
        var accountTargets = new HashSet<int> { MetadataTokens.GetToken(account) };

        void RequireRoute(TypeDefinition type, string factory, HashSet<int> targets, string[]? arguments = null)
        {
            if (type.GetMethods().Any(method =>
                (arguments is null ? md.StringComparer.Equals(md.GetMethodDefinition(method).Name, factory)
                    : Matches(md, method, factory, arguments)) && Calls(pe, md, method, targets)))
                return;
            throw new InvalidOperationException($"Save routing changed: {md.GetString(type.Name)}.{factory} no longer uses the expected path selector.");
        }

        RequireRoute(FindType(Saves + "Managers", "ProfileSaveManager"), "GetProfileSavePath", accountTargets, [NullableBool]);
        RequireRoute(FindType(Saves + "Managers", "PrefsSaveManager"), "GetPrefsPath", profileTargets, ["System.Int32", NullableBool]);
        RequireRoute(FindType(Saves + "Managers", "ProgressSaveManager"), "GetProgressPathForProfile", profileTargets, ["System.Int32", NullableBool]);
        RequireRoute(FindType(Saves + "Managers", "RunSaveManager"), "GetRunSavePath", profileTargets, ["System.Int32", "System.String", NullableBool]);
        RequireRoute(FindType(Saves + "Managers", "RunHistorySaveManager"), "GetHistoryPath", profileTargets, ["System.Int32", NullableBool]);
        var accountBase = provider.GetMethods()
            .Where(method => md.StringComparer.Equals(md.GetMethodDefinition(method).Name, "GetAccountScopedBasePath"))
            .Select(method => MetadataTokens.GetToken(method)).ToHashSet();
        RequireRoute(FindType(Saves.TrimEnd('.'), "SaveManager"), "ConstructDefault", accountBase);
        return md.GetGuid(md.GetModuleDefinition().Mvid);
    }

    private static MethodDefinitionHandle FindSelector(MetadataReader md, TypeDefinition type, string name, string[] arguments)
    {
        var candidates = type.GetMethods().Where(handle => Matches(md, handle, name, arguments)).ToArray();
        if (candidates.Length != 1 || md.GetMethodDefinition(candidates[0]).RelativeVirtualAddress == 0)
            throw new MissingMethodException("Expected one managed relative save selector: " + name);
        return candidates[0];
    }

    private static bool Matches(MetadataReader md, MethodDefinitionHandle handle, string name, string[] arguments)
    {
        var method = md.GetMethodDefinition(handle);
        if (!md.StringComparer.Equals(method.Name, name) || (method.Attributes & MethodAttributes.Static) == 0)
            return false;
        var signature = method.DecodeSignature(Decoder, (object?)null);
        return !signature.Header.IsInstance && signature.GenericParameterCount == 0 &&
            signature.ReturnType == "System.String" && signature.ParameterTypes.SequenceEqual(arguments);
    }

    private static bool Calls(PEReader pe, MetadataReader md, MethodDefinitionHandle handle, HashSet<int> targets)
    {
        var method = md.GetMethodDefinition(handle);
        if (method.RelativeVirtualAddress == 0) return false;
        var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
        return il is not null && IlReader.Read(il).Any(instruction =>
            instruction.Code == OpCodes.Call && instruction.Operand is int token && targets.Contains(token));
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
