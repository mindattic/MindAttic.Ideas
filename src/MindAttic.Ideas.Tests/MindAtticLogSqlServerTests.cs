using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MindAttic.Log;
using MindAttic.Log.Extensions;
using MindAttic.Log.Schema;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// The LIVE proof that AddIdeasCore's MindAttic.Log wiring (DependencyInjection/ServiceCollectionExtensions.cs)
/// actually reaches a real SQL Server table, not just that it compiles against the SqlServer sink's API.
/// [Explicit] for the same reason PageHistorySqlServerTests is: needs a live SQL Server/LocalDB instance,
/// which isn't assumed to exist in every environment this suite runs in. Unlike that test, this one
/// provisions and drops its own throwaway database rather than depending on the pre-seeded dev DB — the
/// MindAttic_Log table has no dependency on CMS content.
/// </summary>
[TestFixture]
public class MindAtticLogSqlServerTests
{
    private const string Master = @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True";
    private string databaseName = null!;
    private string connectionString = null!;

    [SetUp]
    public async Task SetUp()
    {
        databaseName = "MindAtticLogTest_" + Guid.NewGuid().ToString("N");
        connectionString = $@"Server=(localdb)\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";

        await using var master = new SqlConnection(Master);
        await master.OpenAsync();
        await using var create = master.CreateCommand();
        create.CommandText = $"CREATE DATABASE [{databaseName}];";
        await create.ExecuteNonQueryAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await using var master = new SqlConnection(Master);
        await master.OpenAsync();
        await using var drop = master.CreateCommand();
        drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];";
        await drop.ExecuteNonQueryAsync();
    }

    [Test]
    [Explicit("Requires SQL Server LocalDB — see PageHistorySqlServerTests for the same convention.")]
    public async Task AddMindAtticLog_SqlServer_Writes_A_Readable_Row_Through_ILogger()
    {
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = LogSchema.CreateTableSqlServer; // same statement Program.cs runs at startup
            await command.ExecuteNonQueryAsync();
        }

        var services = new ServiceCollection();
        services.AddMindAtticLog(o =>
        {
            o.Application = "Ideas";
            o.Destination = LogDestination.SqlServer;
            o.SqlServerConnectionString = connectionString;
        });

        using (var provider = services.BuildServiceProvider())
        {
            var logger = provider.GetRequiredService<ILogger<MindAtticLogSqlServerTests>>();
            logger.LogWarning("Ideas SQL Server tier integration test at {Utc}", DateTime.UtcNow);
        } // Dispose flushes the MSSqlServer sink (AddSerilog(..., dispose: true))

        await using var verify = new SqlConnection(connectionString);
        await verify.OpenAsync();
        await using var select = verify.CreateCommand();
        select.CommandText = $"SELECT Application, Level, Message FROM dbo.{LogSchema.TableName};";
        await using var reader = await select.ExecuteReaderAsync();

        Assert.That(await reader.ReadAsync(), Is.True, "Expected the logged warning to have reached the table.");
        Assert.Multiple(() =>
        {
            Assert.That(reader.GetString(0), Is.EqualTo("Ideas"));
            Assert.That(reader.GetString(2), Does.Contain("Ideas SQL Server tier integration test"));
        });
    }
}
