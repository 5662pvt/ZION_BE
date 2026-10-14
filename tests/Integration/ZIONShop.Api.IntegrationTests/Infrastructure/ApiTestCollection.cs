namespace ZIONShop.Api.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public class ApiTestCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "ApiIntegrationTests";
}
