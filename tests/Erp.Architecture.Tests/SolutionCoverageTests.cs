using static Erp.Architecture.Tests.SolutionAssemblies;

namespace Erp.Architecture.Tests;

/// <summary>The rules only see projects this test project references; make sure none are missed.</summary>
public class SolutionCoverageTests
{
    [Fact]
    public void Every_src_project_is_inspected()
    {
        var missing = Directory
            .GetFiles(Path.Combine(RepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !File.Exists(PathOf(name!)))
            .ToList();

        Assert.True(missing.Count == 0, "Reference these projects from Erp.Architecture.Tests: " + string.Join(", ", missing));
    }

    [Fact]
    public void At_least_one_module_is_found()
    {
        Assert.NotEmpty(ModuleNames);
    }
}
