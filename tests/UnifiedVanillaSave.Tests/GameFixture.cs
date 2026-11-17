// Static-analysis/contract fixtures. Not a substitute for a real game-DLL MOD build.
namespace MegaCrit.Sts2.Core.Saves
{
    public static class UserDataPathProvider
    {
        public static bool IsRunningModded { get; set; }
        public static string GetProfileDir(int id, bool? forceModState) =>
            (forceModState ?? IsRunningModded ? "modded/" : "") + "profile" + id;
        public static string GetProfileDir(int id) => GetProfileDir(id, null);
        public static string GetProfileScopedPath(int id, string dataType,
            Platform.PlatformType? platformOverride = null, ulong? userIdOverride = null) =>
            GetProfileScopedBasePath(id, platformOverride, userIdOverride) + "/" + dataType;
        public static string GetProfileScopedBasePath(int id,
            Platform.PlatformType? platformOverride = null, ulong? userIdOverride = null) =>
            $"user://{(platformOverride == Platform.PlatformType.Steam ? "steam" : "default")}/{userIdOverride ?? 1}/{GetProfileDir(id)}";
    }
}
namespace MegaCrit.Sts2.Core.Platform
{
    public enum PlatformType { Steam, Other }
}
