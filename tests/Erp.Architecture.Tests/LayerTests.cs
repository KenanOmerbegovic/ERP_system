using static Erp.Architecture.Tests.SolutionAssemblies;

namespace Erp.Architecture.Tests;

/// <summary>CLAUDE.md: inside a module, Endpoints -> Application -> Domain; Domain depends only on SharedKernel.</summary>
public class LayerTests
{
    [Theory]
    [MemberData(nameof(ModuleData), MemberType = typeof(SolutionAssemblies))]
    public void Domain_depends_only_on_SharedKernel(string module)
    {
        var domain = $"{ModuleAssembly(module)}.Domain";

        var result = TypesIn(ModuleAssembly(module))
            .That().ResideInNamespace(domain)
            .Should().OnlyHaveDependenciesOn("System", SharedKernelAssembly, domain)
            .GetResult();

        ArchAssert.Passes(result, $"{domain} may depend only on {SharedKernelAssembly} and System");
    }

    [Theory]
    [MemberData(nameof(ModuleData), MemberType = typeof(SolutionAssemblies))]
    public void Domain_does_not_use_data_access_or_networking(string module)
    {
        var domain = $"{ModuleAssembly(module)}.Domain";

        var result = TypesIn(ModuleAssembly(module))
            .That().ResideInNamespace(domain)
            .ShouldNot().HaveDependencyOnAny("System.Data", "System.Net")
            .GetResult();

        ArchAssert.Passes(result, $"{domain} must not use data access or networking");
    }

    [Theory]
    [MemberData(nameof(ModuleData), MemberType = typeof(SolutionAssemblies))]
    public void Application_does_not_depend_on_Endpoints(string module)
    {
        var result = TypesIn(ModuleAssembly(module))
            .That().ResideInNamespace($"{ModuleAssembly(module)}.Application")
            .ShouldNot().HaveDependencyOn($"{ModuleAssembly(module)}.Endpoints")
            .GetResult();

        ArchAssert.Passes(result, "Application must not depend on Endpoints");
    }

    [Theory]
    [MemberData(nameof(ModuleData), MemberType = typeof(SolutionAssemblies))]
    public void Application_does_not_depend_on_Infrastructure(string module)
    {
        var result = TypesIn(ModuleAssembly(module))
            .That().ResideInNamespace($"{ModuleAssembly(module)}.Application")
            .ShouldNot().HaveDependencyOn($"{ModuleAssembly(module)}.Infrastructure")
            .GetResult();

        ArchAssert.Passes(result, "Application defines interfaces; Infrastructure implements them, not the other way round");
    }

    /// <summary>Guards the layer rules above against silently matching nothing (e.g. types in sub-namespaces).</summary>
    [Theory]
    [MemberData(nameof(ModuleData), MemberType = typeof(SolutionAssemblies))]
    public void Layer_rules_see_every_layer_type(string module)
    {
        foreach (var layer in new[] { "Domain", "Application", "Infrastructure", "Endpoints" })
        {
            var layerNamespace = $"{ModuleAssembly(module)}.{layer}";
            var expected = TypesIn(ModuleAssembly(module)).GetTypes()
                .Count(type => type.Namespace == layerNamespace || type.Namespace?.StartsWith(layerNamespace + ".", StringComparison.Ordinal) == true);
            var selected = TypesIn(ModuleAssembly(module)).That().ResideInNamespace(layerNamespace).GetTypes().Count();

            Assert.True(expected == selected, $"{layerNamespace}: rule selects {selected} of {expected} types");
        }
    }

    [Fact]
    public void SharedKernel_depends_only_on_System()
    {
        var result = TypesIn(SharedKernelAssembly)
            .Should().OnlyHaveDependenciesOn("System", SharedKernelAssembly)
            .GetResult();

        ArchAssert.Passes(result, $"{SharedKernelAssembly} is what Domain depends on, so it may depend only on System");
    }
}
