using CrawlerWebApi.Services;
using IC.Test.Playwright.Crawler.SignalR;
using Microsoft.AspNetCore.SignalR;
using NLog;
using NLog.Web;
using System.Reflection.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;
using IC.Test.Playwright.Crawler.Providers.Logger;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Load configuration from 'config/appsettings.json' and environment-specific files
builder.Configuration

    // Ensure the root directory is used
    .SetBasePath(Directory.GetCurrentDirectory())
    
    // Load main appsettings.json
    .AddJsonFile("config/appsettings.json", optional: false, reloadOnChange: true)
    
    // Load environment-specific settings
    .AddJsonFile($"config/appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    
    // Optionally load environment variables
    .AddEnvironmentVariables(); 

// Add services to the container.
// Following options will enfore serialization of json data with pascal casing
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure System.Text.Json to use PascalCase
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    })
    .AddNewtonsoftJson(options =>
    {
        // Configure Newtonsoft.Json to use PascalCase
        options.SerializerSettings.ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = null // Keep PascalCase
        };
    });

// Add SignalR service
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(2); // Time client can remain unresponsive before timeout
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);     // Maximum time to wait for the initial handshake
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);    // Frequency of keep-alive messages sent by server
});

// DI (Dependency Injection)
builder.Services.AddProjectDependencies();

// Load NLog configuration
NLog.LogManager.Setup().LoadConfigurationFromFile("nlog.config");

// Get url from appsettings
var allowedOrigin = builder.Configuration.GetValue<string>("CorsSettings:AllowedOrigin");

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder => builder.WithOrigins(allowedOrigin)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials()); // Allow credentials like cookies, headers, etc.
});

// Register SvnService with IConfiguration support
builder.Services.AddSingleton<SvnService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();




// -
// --
// ---
// ----
// ----- Build
// ----
// ---
// --
// -

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Apply the CORS policy globally
app.UseCors("AllowSpecificOrigin");

app.UseHttpsRedirection();

app.UseAuthorization();

// Set IHubContext for SignalRLogger (to make the static method work)
var hubContext = app.Services.GetRequiredService<IHubContext<LoggingHub>>();
SignalRLogger.SetHubContext(hubContext);
app.MapHub<LoggingHub>("/loggingHub");

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var loggingProvider = services.GetRequiredService<ILoggingProvider>();

    // Use loggingProvider here
    loggingProvider.SystemLog(Microsoft.Extensions.Logging.LogLevel.Information,"Application started.");
}

app.Run();

