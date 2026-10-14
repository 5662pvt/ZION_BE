using NetArchTest.Rules;

namespace ZIONShop.ArchitectureTests;

internal static class ArchitectureAssert
{
    public static string Describe(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : $"Violating types: {string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>())}";
}
