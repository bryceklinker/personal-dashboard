using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Personal.Dashboard.Core;
using Personal.Dashboard.Core.Common.Storage;
using Personal.Dashboard.Test.Support;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Api.Host.Tests.Support;

public class PersonalDashboardApiApplication : WebApplicationFactory<Program>
{
    public const string FootballApiBaseUrl = "https://football.api.test";
    private readonly string _databaseName = $"{Guid.NewGuid():N}";

    public FakeHttpMessageHandler HttpHandler =>
        Services.GetRequiredService<FakeHttpMessageHandler>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.AddPersonalDashboardCore(opts =>
            {
                opts.ConfigureDbContext = ctx => ctx.UseInMemoryDatabase(_databaseName);
                opts.ConfigureFootballApi = api => api.BaseUrl = FootballApiBaseUrl;
            });
            services.AddPersonalDashboardTestingServices();
        });
    }

    public async Task AddToDbAsync<T>(T entity) where T : notnull
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PersonalDashboardContext>();
        context.Add(entity);
        await context.SaveChangesAsync();
    }
}