// This fixture tests reflection validation only. It is not a game API substitute for MOD builds.
namespace MegaCrit.Sts2.Core.Saves;
public static class UserDataPathProvider
{
    public static string GetProfileDir(int id) => "test-fixture-" + id;
}
