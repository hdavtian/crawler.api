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

// Add authentication & authorization
builder.Services.ConfigureAuthentication(builder.Configuration);
builder.Services.AddAuthorization(); // Needed to enforce authentication on protected endpoints

// DI (Dependency Injection)
builder.Services.AddProjectDependencies();

// Load NLog configuration
NLog.LogManager.Setup().LoadConfigurationFromFile("nlog.config");

// Get url array from appsettings
var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
        builder => builder.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials()); // Allow credentials like cookies, headers, etc.
});

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
app.UseCors("CorsPolicy");

app.UseHttpsRedirection();
app.UseAuthentication();
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

