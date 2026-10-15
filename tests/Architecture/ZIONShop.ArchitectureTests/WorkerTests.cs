using System.Reflection;
using NetArchTest.Rules;

namespace ZIONShop.ArchitectureTests;

public class WorkerTests
{
    [Fact]
    public void Worker_Should_Not_Depend_On_Any_Module_Domain()
    {
        var worker = Assembly.Load("ZIONShop.Worker");
        var forbidden = ModuleCatalog.Names.Select(m => $"ZIONShop.{m}.Domain").ToArray();

        var result = Types.InAssembly(worker).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        result.IsSuccessful.Should().BeTrue(ArchitectureAssert.Describe(result));
    }
}
