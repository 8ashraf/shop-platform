using System.Reflection;
using NetArchTest.Rules;

namespace Shop.ArchitectureTests;

/// <summary>
/// Discovers every module from the assemblies copied into the test output, so
/// modules added with modmono.sh are covered automatically.
/// </summary>
public class ModuleBoundaryTests
{
    private const string ModulesRoot = "Shop.Modules";
    private static readonly string[] InternalLayers = ["Domain", "Application", "Infrastructure", "Presentation"];

    /// <summary>Empty string = "no modules yet"; xUnit fails a theory that has no data.</summary>
    private const string NoModules = "";

    public static TheoryData<string> Modules()
    {
        var data = new TheoryData<string>();
        foreach (var module in DiscoverModules())
        {
            data.Add(module);
        }

        if (data.Count == 0)
        {
            data.Add(NoModules);
        }

        return data;
    }

    [Fact]
    public void Shared_building_blocks_do_not_depend_on_modules()
    {
        var shared = new[] { "Kernel", "Application", "Infrastructure" }
            .Select(name => Assembly.Load($"Shop.Shared.{name}"))
            .ToArray();

        var result = Types.InAssemblies(shared)
            .ShouldNot()
            .HaveDependencyOn(ModulesRoot)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_does_not_depend_on_other_modules_internals(string module)
    {
        if (module == NoModules)
        {
            return;
        }

        var forbidden = DiscoverModules()
            .Where(other => other != module)
            .SelectMany(other => InternalLayers.Select(layer => $"{ModulesRoot}.{other}.{layer}"))
            .ToArray();

        if (forbidden.Length == 0)
        {
            return;
        }

        var result = Types.InAssemblies(LoadModule(module, [.. InternalLayers, "IntegrationEvents"]))
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_does_not_depend_on_outer_layers(string module) =>
        AssertNoDependency(module, "Domain", "Application", "Infrastructure", "Presentation");

    [Theory]
    [MemberData(nameof(Modules))]
    public void Application_does_not_depend_on_infrastructure_or_presentation(string module) =>
        AssertNoDependency(module, "Application", "Infrastructure", "Presentation");

    [Theory]
    [MemberData(nameof(Modules))]
    public void Presentation_does_not_depend_on_infrastructure(string module) =>
        AssertNoDependency(module, "Presentation", "Infrastructure");

    [Theory]
    [MemberData(nameof(Modules))]
    public void IntegrationEvents_only_depend_on_shared_kernel(string module) =>
        AssertNoDependency(module, "IntegrationEvents", "Domain", "Application", "Infrastructure", "Presentation");

    private static void AssertNoDependency(string module, string layer, params string[] forbiddenLayers)
    {
        if (module == NoModules)
        {
            return;
        }

        var result = Types.InAssembly(Assembly.Load($"{ModulesRoot}.{module}.{layer}"))
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenLayers.Select(l => $"{ModulesRoot}.{module}.{l}").ToArray())
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static Assembly[] LoadModule(string module, IEnumerable<string> layers) =>
        layers.Select(layer => Assembly.Load($"{ModulesRoot}.{module}.{layer}")).ToArray();

    private static List<string> DiscoverModules() =>
        Directory.GetFiles(AppContext.BaseDirectory, $"{ModulesRoot}.*.Domain.dll")
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .Select(name => name[(ModulesRoot.Length + 1)..^".Domain".Length])
            .Order(StringComparer.Ordinal)
            .ToList();

    private static string Describe(TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
}
