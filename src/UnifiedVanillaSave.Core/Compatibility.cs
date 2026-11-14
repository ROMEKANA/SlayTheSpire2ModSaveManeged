using System.Globalization;
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
    bool CallersAndVanillaLayoutVerified);

public static class Compatibility
{
    public const string TargetType = "MegaCrit.Sts2.Core.Saves.UserDataPathProvider";
    public const string TargetMethod = "GetProfileDir";

    // Candidate supported by reference source; acceptance still requires a local IL/caller audit.
    public static MethodInfo Resolve(Assembly game, AuditConfig config)
    {
        if (!config.CallersAndVanillaLayoutVerified || string.IsNullOrWhiteSpace(config.AuditedGameVersion))
            throw new InvalidOperationException("Local caller/layout audit has not been confirmed.");
        if (!HashFile(game.Location).Equals(config.GameAssemblySha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("sts2.dll changed. Re-audit before enabling this mod.");
        var type = game.GetType(TargetType, throwOnError: true)!;
        var method = type.GetMethod(TargetMethod,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
            binder: null, types: [typeof(int)], modifiers: null);
        if (method is null || method.ReturnType != typeof(string) || method.IsGenericMethod)
            throw new MissingMethodException("Expected audited static string GetProfileDir(int). No patch applied.");
        var il = method.GetMethodBody()?.GetILAsByteArray()
            ?? throw new InvalidOperationException("Target has no readable IL.");
        if (!Hash(il).Equals(config.MethodIlSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Target IL differs from local audit.");
        return method;
    }

    public static string VanillaProfile(int id) => "profile" + id.ToString(CultureInfo.InvariantCulture);
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
