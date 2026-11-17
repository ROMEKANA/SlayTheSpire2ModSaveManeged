using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace UnifiedVanillaSave.Core;

public sealed record AuditConfig(
    string GameAssemblySha256,
    string MethodIlSha256,
    string UserDataRoot,
    string BackupDirectory,
    string AuditedGameVersion,
    bool CallersAndVanillaLayoutVerified,
    Dictionary<string, string>? PathMethodIlSha256 = null);

public static class Compatibility
{
    public const string TargetType = "MegaCrit.Sts2.Core.Saves.UserDataPathProvider";

    public static IReadOnlyList<string> PathMethodNames { get; } =
        Array.AsReadOnly(new[] { "GetAccountDir", "GetProfileDir" });

    public static MethodInfo[] ResolvePaths(Assembly game, AuditConfig config)
    {
        if (!config.CallersAndVanillaLayoutVerified || string.IsNullOrWhiteSpace(config.AuditedGameVersion))
            throw new InvalidOperationException("Local caller/layout audit has not been confirmed.");
        if (!HashFile(game.Location).Equals(config.GameAssemblySha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("sts2.dll changed. Re-audit before enabling this mod.");
        if (config.PathMethodIlSha256 is null || PathMethodNames.Any(n => !config.PathMethodIlSha256.ContainsKey(n)))
            throw new InvalidOperationException("Older wrapper/absolute-path audits cannot enable relative selector patches. Run scripts/Setup.ps1 to regenerate config for the audited DLL.");
        var type = game.GetType(TargetType, throwOnError: true)!;
        return PathMethodNames.Select(name =>
        {
            Type[] arguments = name == "GetAccountDir" ? [typeof(bool?)] : [typeof(int), typeof(bool?)];
            var method = type.GetMethod(name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                binder: null, types: arguments, modifiers: null);
            if (method is null || method.ReturnType != typeof(string) || method.IsGenericMethod)
                throw new MissingMethodException("Audited relative selector signature changed: " + name);
            var il = method.GetMethodBody()?.GetILAsByteArray()
                ?? throw new InvalidOperationException("Target has no readable IL: " + name);
            if (!Hash(il).Equals(config.PathMethodIlSha256[name], StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Relative selector IL differs from local audit: " + name);
            return method;
        }).ToArray(); // Validate BOTH before applying ANY patch.
    }

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static AuditConfig ReadConfig(string path) =>
        JsonSerializer.Deserialize<AuditConfig>(File.ReadAllText(path), new JsonSerializerOptions
        { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Empty audit config.");
}
