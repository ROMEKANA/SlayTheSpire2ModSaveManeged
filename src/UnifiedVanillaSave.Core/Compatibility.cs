using System.Reflection;
using System.Security.Cryptography;

namespace UnifiedVanillaSave.Core;

public static class Compatibility
{
    public const string TargetType = "MegaCrit.Sts2.Core.Saves.UserDataPathProvider";

    public static IReadOnlyList<string> PathMethodNames { get; } =
        Array.AsReadOnly(new[] { "GetAccountDir", "GetProfileDir" });

    public static MethodInfo[] ResolvePaths(Assembly game)
    {
        if (SaveRouteAudit.Validate(game.Location) != game.ManifestModule.ModuleVersionId)
            throw new InvalidOperationException("On-disk game DLL differs from the loaded module. Restart after the update.");
        return FindPaths(game);
    }

    // Probe only the two selectors, with no writes to game state or save storage.
    public static void CheckSelectorBehavior(MethodInfo[] methods)
    {
        static void ExpectPath(MethodInfo method, string expected, params object?[] args)
        {
            var actual = (method.Invoke(null, args) as string)?.Replace('\\', '/');
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new InvalidOperationException($"Save path layout changed in {method.Name}; expected '{expected}', got '{actual}'.");
        }

        var account = methods[0];
        var profile = methods[1];
        ExpectPath(account, "", false);
        ExpectPath(account, "modded", true);
        var defaultAccount = account.Invoke(null, [null]) as string;
        if (defaultAccount is not ("" or "modded"))
            throw new InvalidOperationException("Default account save path is neither vanilla nor modded.");
        foreach (var id in new[] { 1, 2, 3 })
        {
            var vanilla = FormattableString.Invariant($"profile{id}");
            var modded = "modded/" + vanilla;
            ExpectPath(profile, vanilla, id, false);
            ExpectPath(profile, modded, id, true);
            ExpectPath(profile, defaultAccount == "" ? vanilla : modded, id, null);
        }
    }

    private static MethodInfo[] FindPaths(Assembly game)
    {
        var type = game.GetType(TargetType, throwOnError: true)!;
        return PathMethodNames.Select(name =>
        {
            Type[] arguments = name == "GetAccountDir" ? [typeof(bool?)] : [typeof(int), typeof(bool?)];
            var method = type.GetMethod(name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                binder: null, types: arguments, modifiers: null);
            if (method is null || method.ReturnType != typeof(string) || method.IsGenericMethod)
                throw new MissingMethodException("Audited relative selector signature changed: " + name);
            return method;
        }).ToArray();
    }

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
