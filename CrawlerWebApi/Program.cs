using CrawlerWebApi.signalR;
using Microsoft.AspNetCore.SignalR;
using NLog;
using NLog.Web;
using System.Reflection.Metadata;

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
builder.Services.AddControllers();

// Add SignalR service
builder.Services.AddSignalR();

// DI (Dependency Injection)
builder.Services.AddProjectDependencies();

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

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Load NLog configuration
NLog.LogManager.Setup().LoadConfigurationFromFile("nlog.config");

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

app.Run();
