using static Erp.Architecture.Tests.SolutionAssemblies;

namespace Erp.Architecture.Tests;

/// <summary>
/// CLAUDE.md: country rules live only in Erp.Localization.BiH behind module-defined interfaces.
/// Modules define the interfaces; only the composition root (Erp.Api) may reference the implementation.
/// </summary>
public class LocalizationTests
{
    public static TheoryData<string> NonHostAssemblies
    {
        get
        {
            var data = new TheoryData<string> { SharedKernelAssembly };
            foreach (var module in ModuleNames)
            {
                data.Add(ModuleAssembly(module));
                if (Exists(ContractsAssembly(module)))
                {
                    data.Add(ContractsAssembly(module));
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(NonHostAssemblies))]
    public void Only_the_api_references_Localization_BiH(string assemblyName)
    {
        ArchAssert.DoesNotReference(
            assemblyName,
            [LocalizationBiHAssembly],
            $"Only {ApiAssembly} may reference {LocalizationBiHAssembly}; modules use their own interfaces");
    }
}
