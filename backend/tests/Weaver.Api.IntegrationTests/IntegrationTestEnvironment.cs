using Testcontainers.PostgreSql;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// One Postgres container and one API host for the whole run: starting either per test
/// would dominate the run time. Tests isolate themselves by creating their own users and
/// work items instead of relying on an empty database.
/// </summary>
[SetUpFixture]
public class IntegrationTestEnvironment
{
    private static PostgreSqlContainer? _database;
    private static WeaverApiFactory? _factory;

    public static WeaverApiFactory Factory =>
        _factory ?? throw new InvalidOperationException("The integration test environment has not started.");

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        // Matches the image compose.yaml runs.
        _database = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await _database.StartAsync();
        _factory = new WeaverApiFactory(_database.GetConnectionString());
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }
}
