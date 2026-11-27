using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using UnifiedVanillaSave.Core;
using static UnifiedVanillaSave.Core.IlReader;

namespace SaveAudit;

// Reads PE metadata and IL only. No Assembly.Load, game execution, patching or save access.
public static class PathReport
{
    public static void Write(string assemblyPath, TextWriter output)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        output.WriteLine("SAVE PATH DIAGNOSTIC: static evidence only; not proof of runtime routing.");
        output.WriteLine("Assembly SHA256: " + Compatibility.HashFile(assemblyPath));
        output.WriteLine("Module MVID: " + md.GetGuid(md.GetModuleDefinition().Mvid));
        var target = md.TypeDefinitions.FirstOrDefault(h => TypeName(md, h) == Compatibility.TargetType);
        if (target.IsNil) throw new MissingMemberException(Compatibility.TargetType);
        var methods = md.GetTypeDefinition(target).GetMethods().ToArray();
        var tokens = methods.Select(h => MetadataTokens.GetToken(h)).ToHashSet();
        output.WriteLine("\n=== All path-provider methods and IL ===");
        foreach (var handle in methods)
        {
            var method = md.GetMethodDefinition(handle);
            output.WriteLine($"\n0x{MetadataTokens.GetToken(handle):X8} {MethodName(md, handle)} attributes={method.Attributes}");
            if (method.RelativeVirtualAddress == 0) { output.WriteLine("  <no IL>"); continue; }
            var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
            output.WriteLine("  IL SHA256: " + Compatibility.Hash(il));
            foreach (var instruction in Read(il))
                output.WriteLine($"  IL_{instruction.Offset:X4}: {instruction.Code.Name} {Format(md, instruction)}");
        }
        output.WriteLine("\n=== Direct references from outside the provider ===");
        output.WriteLine("Includes calls, function pointers and method tokens. Reflection/indirect calls and cached paths require further audit.");
        foreach (var typeHandle in md.TypeDefinitions)
        {
            if (typeHandle == target) continue;
            foreach (var methodHandle in md.GetTypeDefinition(typeHandle).GetMethods())
            {
                var method = md.GetMethodDefinition(methodHandle);
                if (method.RelativeVirtualAddress == 0) continue;
                foreach (var instruction in Read(pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!))
                {
                    if (instruction.Code.OperandType is not (OperandType.InlineMethod or OperandType.InlineTok)) continue;
                    if (instruction.Operand is not int token) continue;
                    var handle = MetadataTokens.EntityHandle(token);
                    if (handle.Kind == HandleKind.MethodSpecification)
                        handle = md.GetMethodSpecification((MethodSpecificationHandle)handle).Method;
                    if (handle.Kind != HandleKind.MethodDefinition || !tokens.Contains(MetadataTokens.GetToken(handle))) continue;
                    output.WriteLine($"{MethodName(md, methodHandle)} IL_{instruction.Offset:X4}: {instruction.Code.Name} {MethodName(md, (MethodDefinitionHandle)handle)}");
                }
            }
        }
    }

    private static string Format(MetadataReader md, Instruction instruction)
    {
        if (instruction.Operand is not int token) return Convert.ToString(instruction.Operand, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        if (instruction.Code.OperandType == OperandType.InlineString)
            return JsonSerializer.Serialize(md.GetUserString(MetadataTokens.UserStringHandle(token & 0xFFFFFF)));
        if (instruction.Code.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok or OperandType.InlineSig)
            return $"0x{token:X8} {EntityName(md, MetadataTokens.EntityHandle(token))}";
        return token.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string MethodName(MetadataReader md, MethodDefinitionHandle handle)
    {
        var method = md.GetMethodDefinition(handle);
        return TypeName(md, method.GetDeclaringType()) + "." + md.GetString(method.Name) +
            " [signature=" + Convert.ToHexString(md.GetBlobBytes(method.Signature)) + "]";
    }

    private static string TypeName(MetadataReader md, TypeDefinitionHandle handle)
    {
        var type = md.GetTypeDefinition(handle);
        var parent = type.GetDeclaringType();
        return parent.IsNil ? md.GetString(type.Namespace) + "." + md.GetString(type.Name)
            : TypeName(md, parent) + "+" + md.GetString(type.Name);
    }

    private static string EntityName(MetadataReader md, EntityHandle handle) => handle.Kind switch
    {
        HandleKind.MethodDefinition => MethodName(md, (MethodDefinitionHandle)handle),
        HandleKind.TypeDefinition => TypeName(md, (TypeDefinitionHandle)handle),
        HandleKind.TypeReference => TypeReferenceName(md, (TypeReferenceHandle)handle),
        HandleKind.MemberReference => MemberName(md, (MemberReferenceHandle)handle),
        HandleKind.FieldDefinition => FieldName(md, (FieldDefinitionHandle)handle),
        HandleKind.MethodSpecification => EntityName(md, md.GetMethodSpecification((MethodSpecificationHandle)handle).Method) + " <method-spec>",
        HandleKind.TypeSpecification => "<type-spec " + Convert.ToHexString(md.GetBlobBytes(md.GetTypeSpecification((TypeSpecificationHandle)handle).Signature)) + ">",
        _ => "<" + handle.Kind + ">"
    };

    private static string TypeReferenceName(MetadataReader md, TypeReferenceHandle handle)
    {
        var type = md.GetTypeReference(handle);
        return md.GetString(type.Namespace) + "." + md.GetString(type.Name);
    }
    private static string MemberName(MetadataReader md, MemberReferenceHandle handle)
    {
        var member = md.GetMemberReference(handle);
        return EntityName(md, member.Parent) + "." + md.GetString(member.Name) + " [signature=" + Convert.ToHexString(md.GetBlobBytes(member.Signature)) + "]";
    }
    private static string FieldName(MetadataReader md, FieldDefinitionHandle handle)
    {
        var field = md.GetFieldDefinition(handle);
        return TypeName(md, field.GetDeclaringType()) + "." + md.GetString(field.Name);
    }
}
