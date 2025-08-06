using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ContentUnderstanding.Sample.Services;

namespace ContentUnderstanding.Sample;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // Build service provider
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("Azure Content Understanding C# Sample Application");
            logger.LogInformation("==================================================");

            // Parse command line arguments
            var mode = GetArgumentValue(args, "--mode", "interactive");
            
            switch (mode.ToLowerInvariant())
            {
                case "health":
                case "healthcheck":
                    await RunHealthCheckAsync(serviceProvider);
                    break;
                case "interactive":
                default:
                    await RunInteractiveModeAsync(serviceProvider);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Application failed with exception");
            Environment.Exit(1);
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Register application services
        services.AddScoped<HealthCheckService>();
        
        // TODO: Add other services as they are implemented
        // services.AddScoped<ContentUnderstandingService>();
    }

    private static async Task RunHealthCheckAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();

        logger.LogInformation("Running comprehensive health check...");
        logger.LogInformation("");

        var result = await healthCheckService.CheckHealthAsync();
        
        // Display results
        result.DisplayResults(logger);

        // Set exit code based on health status
        if (result.OverallStatus == "Failed")
        {
            Environment.Exit(1);
        }
        else if (result.OverallStatus == "Warning")
        {
            Environment.Exit(2);
        }
    }

    private static async Task RunInteractiveModeAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Interactive mode - Azure Content Understanding Sample");
        logger.LogInformation("");

        // First, run a quick health check
        logger.LogInformation("Performing initial health check...");
        var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();
        var healthResult = await healthCheckService.CheckHealthAsync();
        
        if (healthResult.OverallStatus == "Failed")
        {
            logger.LogError("Health check failed. Please fix issues before proceeding.");
            logger.LogInformation("Run with --mode health for detailed diagnostics.");
            return;
        }
        else if (healthResult.OverallStatus == "Warning")
        {
            logger.LogWarning("Health check completed with warnings. Some features may not work correctly.");
        }
        else
        {
            logger.LogInformation("✅ Health check passed - all services are accessible.");
        }

        logger.LogInformation("");
        logger.LogInformation("Available commands:");
        logger.LogInformation("  --mode health     : Run comprehensive health check");
        logger.LogInformation("  --mode interactive: Interactive mode (default)");
        logger.LogInformation("");
        
        // TODO: Implement interactive menu for Content Understanding operations
        logger.LogInformation("🚧 Content Understanding operations coming in Phase 3...");
        logger.LogInformation("");
        logger.LogInformation("Next steps:");
        logger.LogInformation("1. Run health check: dotnet run -- --mode health");
        logger.LogInformation("2. Check the logs above for any configuration issues");
        logger.LogInformation("3. Ensure all Azure resources are properly deployed");
    }

    private static string GetArgumentValue(string[] args, string argument, string defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(argument, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return defaultValue;
    }
}
