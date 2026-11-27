// Static-analysis/contract fixtures. Not a substitute for a real game-DLL MOD build.
namespace MegaCrit.Sts2.Core.Saves
{
    public static class UserDataPathProvider
    {
        public static bool IsRunningModded { get; set; }
        public static string ModdedDirectory { get; set; } = "modded";
        public static string GetAccountDir(bool? forceModState = null) => (forceModState ?? IsRunningModded) ? ModdedDirectory : "";
        public static string GetProfileDir(int id, bool? forceModState) =>
            Join(GetAccountDir(forceModState), "profile" + id);
        public static string Join(string parent, string child) => string.IsNullOrEmpty(parent) ? child : parent + "/" + child;
        public static string GetProfileDir(int id) => GetProfileDir(id, null);
        public static string GetAccountScopedBasePath(Platform.PlatformType? platform = null) => "user://steam/42";
        public static string GetProfileScopedPath(int id, string dataType,
            Platform.PlatformType? platformOverride = null, ulong? userIdOverride = null) =>
            GetProfileScopedBasePath(id, platformOverride, userIdOverride) + "/" + dataType;
        public static string GetProfileScopedBasePath(int id,
            Platform.PlatformType? platformOverride = null, ulong? userIdOverride = null) =>
            $"user://{(platformOverride == Platform.PlatformType.Steam ? "steam" : "default")}/{userIdOverride ?? 1}/{GetProfileDir(id)}";
    }

    public static class SaveManager
    {
        public static string ConstructDefault() => UserDataPathProvider.GetAccountScopedBasePath(null);
    }
}
namespace MegaCrit.Sts2.Core.Saves.Managers
{
    internal static class ProfileSaveManager
    {
        internal static string GetProfileSavePath(bool? mode) => UserDataPathProvider.Join(UserDataPathProvider.GetAccountDir(mode), "profile.save");
    }
    internal static class PrefsSaveManager
    {
        internal static string GetPrefsPath(int id, bool? mode) => UserDataPathProvider.GetProfileDir(id, mode) + "/saves/prefs.save";
    }
    internal static class ProgressSaveManager
    {
        internal static string GetProgressPathForProfile(int id, bool? mode) => UserDataPathProvider.GetProfileDir(id, mode) + "/saves/progress.save";
    }
    internal static class RunSaveManager
    {
        internal static string GetRunSavePath(int id, string name, bool? mode) => UserDataPathProvider.GetProfileDir(id, mode) + "/saves/" + name;
    }
    internal static class RunHistorySaveManager
    {
        internal static string GetHistoryPath(int id, bool? mode) => UserDataPathProvider.GetProfileDir(id, mode) + "/saves/history";
    }
}
namespace MegaCrit.Sts2.Core.Platform
{
    public enum PlatformType { Steam, Other }
}
