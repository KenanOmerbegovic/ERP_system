using NetArchTest.Rules;

namespace Erp.Architecture.Tests;

internal static class ArchAssert
{
    public static void Passes(TestResult result, string rule)
    {
        var failing = result.FailingTypeNames ?? [];
        Assert.True(result.IsSuccessful, $"{rule}. Violating types:{Environment.NewLine}  " + string.Join(Environment.NewLine + "  ", failing));
    }

    public static void DoesNotReference(string assemblyName, IEnumerable<string> forbidden, string rule)
    {
        var violations = SolutionAssemblies.ReferencesOf(assemblyName).Intersect(forbidden).ToList();
        Assert.True(violations.Count == 0, $"{rule}. {assemblyName} references: {string.Join(", ", violations)}");
    }
}
