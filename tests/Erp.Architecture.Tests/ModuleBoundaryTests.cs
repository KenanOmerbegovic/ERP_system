using static Erp.Architecture.Tests.SolutionAssemblies;

namespace Erp.Architecture.Tests;

/// <summary>CLAUDE.md: modules reference other modules only via their .Contracts project.</summary>
public class ModuleBoundaryTests
{
    private static readonly IReadOnlyList<string> AllModuleInternals = ModuleNames.Select(ModuleAssembly).ToList();

    [Theory]
    [MemberData(nameof(ModuleData), MemberType = typeof(SolutionAssemblies))]
    public void Module_references_other_modules_only_through_contracts(string module)
    {
        ArchAssert.DoesNotReference(
            ModuleAssembly(module),
            AllModuleInternals,
            "Modules may reference other modules only via their .Contracts project");
    }

    [Theory]
    [MemberData(nameof(ModulesWithContractsData), MemberType = typeof(SolutionAssemblies))]
    public void Contracts_do_not_reference_module_internals(string module)
    {
        ArchAssert.DoesNotReference(
            ContractsAssembly(module),
            AllModuleInternals,
            "Contracts must not depend on any module's internals");
    }

    [Theory]
    [MemberData(nameof(ModuleData), MemberType = typeof(SolutionAssemblies))]
    public void Every_module_has_a_contracts_project(string module)
    {
        Assert.True(
            Exists(ContractsAssembly(module)),
            $"Module {module} needs {ContractsAssembly(module)}, referenced from Erp.Architecture.Tests");
    }
}
