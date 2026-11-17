using System.Reflection;
using HarmonyLib;
using UnifiedVanillaSave.Core;

namespace UnifiedVanillaSave;

// Shared with independent Harmony tests; the production MOD still references the game's 0Harmony.dll.
public static class SaveRoutingPatches
{
    private sealed record State(MethodInfo[] Methods, string Owner, Action<string> Log)
    {
        public object Gate { get; } = new();
        public HashSet<string> Logged { get; } = [];
    }
    private static volatile State? active;

    public static void Install(Harmony harmony, MethodInfo[] methods, string owner, Action<string> log)
    {
        if (active is not null) throw new InvalidOperationException("Save routing patches are already installed.");
        if (methods.Length != 2 || !methods.Select(m => m.Name).SequenceEqual(Compatibility.PathMethodNames))
            throw new ArgumentException("Both audited relative selectors are required.", nameof(methods));
        CheckConflicts(methods, owner);
        active = new State(methods, owner, log);
        try
        {
            foreach (var method in methods)
            {
                var prefix = method.Name == "GetAccountDir" ? nameof(BeforeAccount) : nameof(BeforeProfile);
                harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(SaveRoutingPatches), prefix) { priority = Priority.First },
                    postfix: new HarmonyMethod(typeof(SaveRoutingPatches), nameof(AfterRoute)) { priority = Priority.Last });
            }
            CheckConflicts(methods, owner);
        }
        catch { Remove(harmony); throw; }
    }

    public static void Remove(Harmony harmony)
    {
        var state = active;
        if (state is null) return;
        foreach (var method in state.Methods)
        {
            try
            {
                harmony.Unpatch(method, HarmonyPatchType.Prefix, state.Owner);
                harmony.Unpatch(method, HarmonyPatchType.Postfix, state.Owner);
            }
            catch (Exception ex) { state.Log("ROLLBACK_FAILED: exit the game. " + ex); }
        }
        active = null;
    }

    public static void CheckConflicts(MethodInfo[] methods, string owner)
    {
        var type = methods[0].DeclaringType!;
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var related = type.GetMethods(flags).Where(m => m.Name is
            "GetAccountDir" or "GetProfileDir" or "GetProfileScopedPath" or "GetProfileScopedBasePath" or
            "get_IsRunningModded" or "set_IsRunningModded");
        var foreign = related.SelectMany(m => Harmony.GetPatchInfo(m)?.Owners ?? Enumerable.Empty<string>())
            .Where(o => o != owner).Distinct().ToArray();
        if (foreign.Length != 0)
            throw new InvalidOperationException("Conflicting Harmony owners on save path provider: " + string.Join(", ", foreign));
    }

    private static void BeforeAccount(ref bool? __0, out bool __state) => ForceDefault(ref __0, out __state);
    private static void BeforeProfile(ref bool? __1, out bool __state) => ForceDefault(ref __1, out __state);

    private static void ForceDefault(ref bool? requested, out bool changed)
    {
        var state = active ?? throw new InvalidOperationException("Save routing is not initialized.");
        CheckConflicts(state.Methods, state.Owner);
        changed = !requested.HasValue;
        if (changed) requested = false; // Run the original path logic with an explicit vanilla selection.
    }

    private static void AfterRoute(MethodBase __originalMethod, string __result, bool __state)
    {
        if (!__state) return; // Explicit vanilla/modded probes keep their original semantics.
        var state = active ?? throw new InvalidOperationException("Save routing is not initialized.");
        lock (state.Gate)
        {
            var key = __originalMethod.Name + ":" + __result;
            if (state.Logged.Count < 32 && state.Logged.Add(key))
                state.Log($"RELATIVE_ROUTE: {__originalMethod.Name}; requested=default; effective=vanilla; relative='{__result}'. Actual save I/O requires acceptance testing.");
        }
    }
}
