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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using IC.Test.Playwright.Crawler.Interfaces;
using IC.Test.Playwright.Crawler.Providers.Playwright;

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
        services.AddScoped<ILdapAuthService, LdapAuthService>();
        services.AddScoped<ILoggingProvider, NLogProvider>(); // => default logger
        // services.AddScoped<ILoggingProvider, SerilogProvider>(); => example to inject muiltiple dependencies for provider
        services.AddSingleton<WriterQueueService>();
        services.AddSingleton<IPlaywrightFactory, PlaywrightFactory>();
        services.AddSingleton<TestRegistryService>();
        return services;
    }
    
    public static IServiceCollection ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };
            });

        return services;
    }
}
