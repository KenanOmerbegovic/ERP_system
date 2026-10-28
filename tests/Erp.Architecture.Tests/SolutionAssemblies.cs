using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using NetArchTest.Rules;

namespace Erp.Architecture.Tests;

/// <summary>
/// The production assemblies under src/, read from the test output directory.
/// Modules are discovered by name (Erp.Modules.{Name} and Erp.Modules.{Name}.Contracts),
/// so new modules are covered as soon as this project references them.
/// </summary>
internal static partial class SolutionAssemblies
{
    public const string ApiAssembly = "Erp.Api";
    public const string SharedKernelAssembly = "Erp.SharedKernel";
    public const string LocalizationBiHAssembly = "Erp.Localization.BiH";

    public static IReadOnlyList<string> ModuleNames { get; } =
        Directory.GetFiles(AppContext.BaseDirectory, "Erp.Modules.*.dll")
            .Select(path => ModuleInternalsName().Match(Path.GetFileNameWithoutExtension(path)))
            .Where(match => match.Success)
            .Select(match => match.Groups["module"].Value)
            .Order(StringComparer.Ordinal)
            .ToList();

    public static TheoryData<string> ModuleData => ToTheoryData(ModuleNames);

    /// <summary>Modules whose Contracts assembly exists; a missing one is reported by ModuleBoundaryTests.</summary>
    public static TheoryData<string> ModulesWithContractsData =>
        ToTheoryData(ModuleNames.Where(module => Exists(ContractsAssembly(module))));

    public static string ModuleAssembly(string module) => $"Erp.Modules.{module}";

    public static string ContractsAssembly(string module) => $"Erp.Modules.{module}.Contracts";

    public static bool Exists(string assemblyName) => File.Exists(PathOf(assemblyName));

    public static string PathOf(string assemblyName) =>
        Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");

    public static Types TypesIn(string assemblyName) => Types.FromFile(PathOf(assemblyName));

    /// <summary>
    /// Names of the assemblies the compiled assembly references. The compiler only keeps
    /// references that are actually used, so this reflects real dependencies.
    /// </summary>
    public static IReadOnlyList<string> ReferencesOf(string assemblyName)
    {
        using var stream = File.OpenRead(PathOf(assemblyName));
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        return metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
            .ToList();
    }

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Erp.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Erp.sln not found above " + AppContext.BaseDirectory);
    }

    private static TheoryData<string> ToTheoryData(IEnumerable<string> values)
    {
        var data = new TheoryData<string>();
        foreach (var value in values)
        {
            data.Add(value);
        }

        return data;
    }

    [GeneratedRegex(@"^Erp\.Modules\.(?<module>[A-Za-z0-9]+)$")]
    private static partial Regex ModuleInternalsName();
}
