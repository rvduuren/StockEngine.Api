using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MariaDb;

namespace StockEngine.Api.Tests.Integration;

public sealed class StockEngineApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MariaDbContainer _mariaDb = new MariaDbBuilder("mariadb:11.4")
        .Build();

    public async Task InitializeAsync()
    {
        await _mariaDb.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _mariaDb.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        var connectionString = _mariaDb.GetConnectionString();

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
            });
        });

        builder.UseSetting("ConnectionStrings:Default", connectionString);
    }
}