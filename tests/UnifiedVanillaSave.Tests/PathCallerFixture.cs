namespace UnifiedVanillaSave.Tests;

// Static-analysis fixtures only. These are not a substitute for the game's implementations.
public static class PathCallerFixture
{
    public static string ViaOne(int id) => MegaCrit.Sts2.Core.Saves.UserDataPathProvider.GetProfileDir(id);
    public static string ViaTwo(int id) => MegaCrit.Sts2.Core.Saves.UserDataPathProvider.GetProfileDir(id, null);
}
