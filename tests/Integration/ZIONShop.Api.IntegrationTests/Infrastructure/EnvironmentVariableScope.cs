namespace ZIONShop.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Snapshots the current value of each given process environment variable and restores it verbatim
/// (not null) on Dispose. A test that builds its own isolated <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>
/// alongside the shared <see cref="ApiFactory"/> fixture must overwrite real process environment
/// variables for Program.cs's eager config reads (see <see cref="ApiFactory.ConfigureWebHost"/>) --
/// but ApiFactory itself only ever sets them once, lazily, the first time its host is built. Resetting
/// an overwritten variable to null instead of its prior value permanently destroys whatever ApiFactory
/// (or another isolated host) had already put there, breaking any later test in the same process that
/// reads that variable directly.
/// </summary>
public sealed class EnvironmentVariableScope : IDisposable
{
    private readonly Dictionary<string, string?> _originalValues;

    public EnvironmentVariableScope(params string[] variableNames)
    {
        _originalValues = variableNames.ToDictionary(name => name, Environment.GetEnvironmentVariable);
    }

    public void Dispose()
    {
        foreach (var (name, value) in _originalValues)
            Environment.SetEnvironmentVariable(name, value);
    }
}
