using System.Reflection;
using NetArchTest.Rules;

namespace ZIONShop.ArchitectureTests;

public class ApiControllerTests
{
    [Fact]
    public void Controllers_Should_Not_Depend_On_Any_Infrastructure()
    {
        var api = Assembly.Load("ZIONShop.Api");
        var forbidden = ModuleCatalog.Names.Select(m => $"ZIONShop.{m}.Infrastructure").ToArray();

        var result = Types.InAssembly(api)
            .That().ResideInNamespace("ZIONShop.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(ArchitectureAssert.Describe(result));
    }
}
