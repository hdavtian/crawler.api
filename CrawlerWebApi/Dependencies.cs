using CrawlerWebApi.Interfaces;
using IC.Test.Playwright.Crawler.Providers.Logger.Implementations;
using CrawlerWebApi.Services;
using IC.Test.Playwright.Crawler.Drivers;
using IC.Test.Playwright.Crawler.Models;
using IC.Test.Playwright.Crawler.Utility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using System;
using IC.Test.Playwright.Crawler.Providers.Logger;

public static class Dependencies
{
    public static IServiceCollection AddProjectDependencies(this IServiceCollection services)
    {
        // Register instance-based dependencies
        services.AddScoped<CrawlTest>();
        services.AddScoped<DiffTest>();
        services.AddScoped<LoginDriver>();
        services.AddScoped<DiffDriver>();
        services.AddScoped<DiffContext>();
        services.AddScoped<CrawlDriver>();
        services.AddScoped<CrawlContext>();
        services.AddScoped<CrawlArtifactManager>();
        services.AddScoped<DiffArtifactManager>();
        services.AddScoped<CrawlerCommon>();
        services.AddScoped<AxeHelper>();
        services.AddScoped<ITestService, TestService>();
        services.AddScoped<IBaselineTestService, BaselineTestService>();
        services.AddScoped<IDiffTestService, DiffTestService>();
        services.AddScoped<ILoggingProvider, NLogProvider>(); // => default logger
        // services.AddScoped<ILoggingProvider, SerilogProvider>(); => example to inject muiltiple dependencies for provider
        services.AddSingleton<WriterQueueService>();

        // Register PlaywrightContext and IPage
        services.AddScoped<PlaywrightContext>(provider =>
        {
            var playwrightContext = new PlaywrightContext();
            return playwrightContext;
        });

        // Register IPage based on PlaywrightContext
        services.AddScoped<IPage>(provider =>
        {
            var playwrightContext = provider.GetRequiredService<PlaywrightContext>();
            return playwrightContext.Page; // Ensure that Page is initialized when accessed
        });

        return services;
    }
}
