using System.Reflection;
using NetArchTest.Rules;

namespace ZIONShop.ArchitectureTests;

public class ModuleBoundaryTests
{
    public static IEnumerable<object[]> AllModules() =>
        ModuleCatalog.Names.Select(m => new object[] { m });

    private static Assembly LoadModuleAssembly(string module, string layer) =>
        Assembly.Load($"ZIONShop.{module}.{layer}");

    [Theory]
    [MemberData(nameof(AllModules))]
    public void Domain_Should_Not_Depend_On_Own_Application_Or_Infrastructure_Or_EfCore(string module)
    {
        var domain = LoadModuleAssembly(module, "Domain");
        var forbidden = new[]
        {
            $"ZIONShop.{module}.Application",
            $"ZIONShop.{module}.Infrastructure",
            "Microsoft.EntityFrameworkCore",
        };

        var result = Types.InAssembly(domain).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        result.IsSuccessful.Should().BeTrue(ArchitectureAssert.Describe(result));
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void Application_Should_Not_Depend_On_Any_Infrastructure(string module)
    {
        var application = LoadModuleAssembly(module, "Application");
        var forbiddenInfrastructure = ModuleCatalog.Names.Select(m => $"ZIONShop.{m}.Infrastructure").ToArray();

        var result = Types.InAssembly(application).ShouldNot().HaveDependencyOnAny(forbiddenInfrastructure).GetResult();

        result.IsSuccessful.Should().BeTrue(ArchitectureAssert.Describe(result));
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void Module_Should_Not_Depend_On_Other_Modules(string module)
    {
        var forbidden = ModuleCatalog.Names
            .Where(m => m != module)
            .SelectMany(m => new[] { $"ZIONShop.{m}.Domain", $"ZIONShop.{m}.Application", $"ZIONShop.{m}.Infrastructure" })
            .ToArray();

        foreach (var layer in new[] { "Domain", "Application", "Infrastructure" })
        {
            var assembly = LoadModuleAssembly(module, layer);
            var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

            result.IsSuccessful.Should().BeTrue($"{module}.{layer} — {ArchitectureAssert.Describe(result)}");
        }
    }
}
