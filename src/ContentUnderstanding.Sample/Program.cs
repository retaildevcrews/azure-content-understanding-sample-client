using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using ContentUnderstanding.Sample.Services;
using ContentUnderstanding.Sample.Data;

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
                case "analyzers":
                case "list-analyzers":
                    await RunListAnalyzersAsync(serviceProvider);
                    break;
                case "create-analyzer":
                case "create":
                    await RunCreateAnalyzerAsync(serviceProvider);
                    break;
                case "test-analysis":
                case "analyze":
                    await RunTestAnalysisAsync(serviceProvider);
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
        services.AddScoped<ContentUnderstandingService>();
        
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
        
        // Display the full health check results in interactive mode too
        logger.LogInformation("");
        healthResult.DisplayResults(logger);
        
        if (healthResult.OverallStatus == "Failed")
        {
            logger.LogError("❌ Health check failed. Please fix issues before proceeding.");
            logger.LogInformation("Run with --mode health for detailed diagnostics.");
            return;
        }
        else if (healthResult.OverallStatus == "Warning")
        {
            logger.LogWarning("⚠️ Health check completed with warnings. Some features may not work correctly.");
        }
        else
        {
            logger.LogInformation("✅ Health check passed - all services are accessible.");
        }

        logger.LogInformation("");
        logger.LogInformation("Available commands:");
        logger.LogInformation("  --mode health           : Run comprehensive health check");
        logger.LogInformation("  --mode interactive      : Interactive mode (default)");
        logger.LogInformation("  --mode analyzers        : List all analyzers");
        logger.LogInformation("  --mode create-analyzer  : Create sample analyzer");
        logger.LogInformation("  --mode test-analysis    : Test document analysis");
        logger.LogInformation("");
        
        // TODO: Implement interactive menu for Content Understanding operations
        logger.LogInformation("🚧 Content Understanding operations ready for testing...");
        logger.LogInformation("");
        logger.LogInformation("Next steps:");
        logger.LogInformation("1. Run health check: dotnet run -- --mode health");
        logger.LogInformation("2. List analyzers: dotnet run -- --mode analyzers");
        logger.LogInformation("3. Create sample analyzer: dotnet run -- --mode create-analyzer");
        logger.LogInformation("4. Check the logs above for any configuration issues");
        logger.LogInformation("5. Ensure all Azure resources are properly deployed");
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

    private static async Task RunListAnalyzersAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();

        logger.LogInformation("📋 Listing all analyzers...");
        logger.LogInformation("");

        try
        {
            var result = await contentUnderstandingService.ListAnalyzersAsync();
            logger.LogInformation("✅ Analyzers retrieved successfully:");
            logger.LogInformation("{Result}", result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to list analyzers");
            Environment.Exit(1);
        }
    }

    private static async Task RunCreateAnalyzerAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();
        
        logger.LogInformation("📝 Creating sample analyzer from JSON files...");
        logger.LogInformation("");

        try
        {
            // Get available analyzers from JSON files
            var availableAnalyzers = SampleAnalyzers.GetAvailableAnalyzers();
            
            if (availableAnalyzers.Count == 0)
            {
                logger.LogWarning("⚠️ No analyzer JSON files found in Data folder");
                return;
            }

            logger.LogInformation("📂 Available analyzers:");
            foreach (var analyzer in availableAnalyzers)
            {
                logger.LogInformation("   • {Name} (from {FileName})", analyzer.Key, analyzer.Value);
            }
            logger.LogInformation("");

            // Try to create the receipt analyzer specifically, or fall back to first available
            var targetAnalyzer = availableAnalyzers.ContainsKey("receipt") 
                ? availableAnalyzers["receipt"] 
                : availableAnalyzers.First().Value;
            var analyzerName = availableAnalyzers.ContainsKey("receipt") 
                ? "receipt" 
                : availableAnalyzers.First().Key;
            var fileName = targetAnalyzer;

            logger.LogInformation("🔄 Loading analyzer definition: {FileName}", fileName);
            
            var jsonContent = await SampleAnalyzers.LoadAnalyzerJsonAsync(fileName);
            
            // Validate the JSON
            if (!SampleAnalyzers.ValidateAnalyzerJson(jsonContent))
            {
                logger.LogError("❌ Invalid analyzer JSON format in file: {FileName}", fileName);
                return;
            }

            logger.LogInformation("✅ JSON validation passed");
            logger.LogInformation("🚀 Creating analyzer: {AnalyzerName}", analyzerName);

            // Create the analyzer using the Content Understanding service
            var result = await contentUnderstandingService.CreateOrUpdateAnalyzerAsync(analyzerName, jsonContent);
            
            logger.LogInformation("✅ Successfully created analyzer: {AnalyzerName}", analyzerName);
            logger.LogDebug("API Response: {Result}", result);
        }
        catch (FileNotFoundException ex)
        {
            logger.LogError("❌ Analyzer JSON file not found: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to create analyzer");
        }
    }

    private static async Task RunTestAnalysisAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();

        logger.LogInformation("🔍 Test document analysis mode...");
        logger.LogInformation("");

        try
        {
            // Look for receipt1.pdf in the SampleDocuments folder
            var projectRoot = Directory.GetCurrentDirectory();
            var sampleDocumentsPath = Path.Combine(projectRoot, "Data", "SampleDocuments");
            var receiptFilePath = Path.Combine(sampleDocumentsPath, "receipt1.pdf");
            
            if (!File.Exists(receiptFilePath))
            {
                logger.LogError("❌ Sample document not found: {FilePath}", receiptFilePath);
                logger.LogInformation("📄 Please ensure receipt1.pdf is in the Data/SampleDocuments folder");
                return;
            }

            logger.LogInformation("📄 Found sample document: {FileName}", "receipt1.pdf");
            logger.LogInformation("📁 Document path: {FilePath}", receiptFilePath);

            // Read the document file
            var documentData = await File.ReadAllBytesAsync(receiptFilePath);
            logger.LogInformation("📊 Document size: {Size} bytes", documentData.Length);

            // Use the receipt analyzer we created
            var analyzerName = "receipt";
            logger.LogInformation("� Analyzing document with analyzer: {AnalyzerName}", analyzerName);

            // Submit document for analysis
            var analysisResult = await contentUnderstandingService.AnalyzeDocumentAsync(
                analyzerName, 
                documentData, 
                "application/pdf");

            logger.LogInformation("✅ Document analysis submitted successfully!");
            logger.LogInformation("📊 Analysis Result: {Result}", analysisResult.responseContent);

            // Check if we got an operation location URL for polling
            if (!string.IsNullOrEmpty(analysisResult.operationLocation))
            {
                logger.LogInformation("🔄 Operation Location: {OperationLocation}", analysisResult.operationLocation);
                logger.LogInformation("⏳ Polling for analysis results...");
                
                // Poll for results using the operation location URL
                for (int attempt = 1; attempt <= 10; attempt++)
                {
                    await Task.Delay(3000); // Wait 3 seconds between polls
                    
                    logger.LogInformation("🔄 Polling attempt {Attempt}/10...", attempt);
                    
                    try
                    {
                        var resultResponse = await contentUnderstandingService.GetAnalysisResultByLocationAsync(analysisResult.operationLocation);
                        var resultDoc = System.Text.Json.JsonDocument.Parse(resultResponse);
                        var currentStatus = resultDoc.RootElement.GetProperty("status").GetString();
                        
                        logger.LogInformation("📊 Status: {Status}", currentStatus);
                        
                        if (currentStatus == "Succeeded")
                        {
                            logger.LogInformation("🎉 Analysis completed successfully!");
                            logger.LogInformation("📋 Final Results: {Results}", resultResponse);
                            break;
                        }
                        else if (currentStatus == "Failed")
                        {
                            logger.LogError("❌ Analysis failed");
                            logger.LogInformation("📋 Error Details: {Results}", resultResponse);
                            break;
                        }
                        else if (currentStatus == "Running")
                        {
                            logger.LogInformation("⏳ Analysis still in progress...");
                        }
                    }
                    catch (Exception pollEx)
                    {
                        logger.LogError(pollEx, "❌ Error during polling attempt {Attempt}", attempt);
                        if (attempt == 10) // Last attempt
                        {
                            logger.LogError("❌ Failed to get results after 10 attempts");
                        }
                    }
                }
            }
            else
            {
                logger.LogWarning("⚠️ No Operation-Location header received. Cannot poll for results.");
                // Fallback: try to parse the operation ID from the response content
                try
                {
                    var analysisResponse = System.Text.Json.JsonDocument.Parse(analysisResult.responseContent);
                    var operationId = analysisResponse.RootElement.GetProperty("id").GetString();
                    logger.LogInformation("🔄 Fallback: Found Operation ID in response: {OperationId}", operationId);
                    logger.LogInformation("💡 Consider checking the Azure portal or using the original GetAnalysisResultAsync method");
                }
                catch (Exception parseEx)
                {
                    logger.LogError(parseEx, "❌ Could not parse operation ID from response");
                }
            }
        }
        catch (FileNotFoundException ex)
        {
            logger.LogError("❌ Document file not found: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to analyze document");
        }
    }
}
