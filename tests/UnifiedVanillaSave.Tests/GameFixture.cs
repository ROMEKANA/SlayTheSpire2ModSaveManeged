// This fixture tests reflection validation only. It is not a game API substitute for MOD builds.
namespace MegaCrit.Sts2.Core.Saves;
public static class UserDataPathProvider
{
    // An extra overload exercises exact binding without asserting a real game contract.
    public static string GetProfileDir(int id, bool? flag) => "other-fixture-" + id + flag;
    public static string GetProfileDir(int id) => "test-fixture-" + id;
}
