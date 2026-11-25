using System.Xml.Linq;
using static Erp.Architecture.Tests.SolutionAssemblies;

namespace Erp.Architecture.Tests;

/// <summary>
/// CLAUDE.md: modules reference other modules only via their .Contracts project. ModuleBoundaryTests checks the compiled
/// assemblies, where the compiler drops unused references; this checks the project files, so a module cannot even be
/// set up to use another module's implementation. Synchronous queries and commands between modules go through
/// interfaces in the providing module's .Contracts project.
/// </summary>
public class ProjectReferenceTests
{
    private static readonly IReadOnlyList<string> ModuleProjectNames =
        Directory.GetFiles(Path.Combine(RepositoryRoot(), "src", "Modules"), "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .Order(StringComparer.Ordinal)
            .ToList();

    public static TheoryData<string> ModuleProjects
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var project in ModuleProjectNames)
            {
                data.Add(project);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ModuleProjects))]
    public void Module_projects_reference_other_modules_only_through_contracts(string project)
    {
        var forbidden = ModuleReferences(project).Where(reference => !reference.EndsWith(".Contracts", StringComparison.Ordinal)).ToList();

        Assert.True(forbidden.Count == 0, $"{project} references module implementation projects: {string.Join(", ", forbidden)}");
    }

    [Theory]
    [MemberData(nameof(ModuleProjects))]
    public void Contracts_projects_reference_no_module_implementation(string project)
    {
        if (!project.EndsWith(".Contracts", StringComparison.Ordinal))
        {
            return;
        }

        var references = ProjectReferences(project).ToList();
        var allowed = references.Where(r => r == SharedKernelAssembly || (r.StartsWith("Erp.Modules.", StringComparison.Ordinal) && r.EndsWith(".Contracts", StringComparison.Ordinal)));

        Assert.True(references.SequenceEqual(allowed), $"{project} may reference only {SharedKernelAssembly} and other .Contracts projects: {string.Join(", ", references)}");
    }

    [Fact]
    public void The_rule_sees_the_module_projects()
    {
        Assert.Contains("Erp.Modules.Warehouse", ModuleProjectNames);
        Assert.Contains("Erp.Modules.Core.Contracts", ModuleReferences("Erp.Modules.Warehouse"));
    }

    /// <summary>Referenced Erp.Modules.* projects other than the module's own Contracts.</summary>
    private static IEnumerable<string> ModuleReferences(string project)
    {
        var module = project.EndsWith(".Contracts", StringComparison.Ordinal) ? project[..^".Contracts".Length] : project;
        return ProjectReferences(project)
            .Where(reference => reference.StartsWith("Erp.Modules.", StringComparison.Ordinal) && reference != module + ".Contracts");
    }

    private static IEnumerable<string> ProjectReferences(string project)
    {
        var path = Directory.GetFiles(Path.Combine(RepositoryRoot(), "src", "Modules"), project + ".csproj", SearchOption.AllDirectories).Single();
        return XDocument.Load(path).Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')));
    }
}
