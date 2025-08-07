using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
            var analyzerFile = GetArgumentValue(args, "--analyzer-file", "");
            var analyzerName = GetArgumentValue(args, "--analyzer", "");
            var documentFile = GetArgumentValue(args, "--document", "");
            var operationId = GetArgumentValue(args, "--operation-id", "");
            
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
                    await RunCreateAnalyzerAsync(serviceProvider, analyzerName, analyzerFile);
                    break;
                case "test-analysis":
                case "analyze":
                    await RunTestAnalysisAsync(serviceProvider, analyzerName, documentFile);
                    break;
                case "check-operation":
                case "operation":
                    await RunCheckOperationAsync(serviceProvider, operationId);
                    break;
                case "help":
                case "--help":
                case "-h":
                    DisplayHelpInformation(logger);
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
        logger.LogInformation("  --mode check-operation  : Check specific operation status");
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

    /// <summary>
    /// Extracts a readable value from a Content Understanding field element
    /// </summary>
    private static string ExtractFieldValue(JsonElement field)
    {
        try
        {
            // Handle different field types based on the "type" property
            if (field.TryGetProperty("type", out var typeElement))
            {
                var fieldType = typeElement.GetString();
                
                return fieldType?.ToLowerInvariant() switch
                {
                    "string" when field.TryGetProperty("valueString", out var stringValue) => 
                        stringValue.GetString() ?? "N/A",
                    
                    "number" when field.TryGetProperty("valueNumber", out var numberValue) => 
                        numberValue.GetDouble().ToString("F2"),
                    
                    "array" when field.TryGetProperty("valueArray", out var arrayValue) => 
                        ExtractArrayValue(arrayValue),
                    
                    "object" when field.TryGetProperty("valueObject", out var objectValue) => 
                        ExtractObjectValue(objectValue),
                    
                    _ => field.ToString() // Fallback to raw JSON
                };
            }
            
            // Fallback: try common properties
            if (field.TryGetProperty("valueString", out var fallbackString))
                return fallbackString.GetString() ?? "N/A";
            
            if (field.TryGetProperty("content", out var content))
                return content.GetString() ?? "N/A";
            
            return field.ToString();
        }
        catch
        {
            return "Parse Error";
        }
    }

    /// <summary>
    /// Extracts a readable value from an array field
    /// </summary>
    private static string ExtractArrayValue(JsonElement arrayElement)
    {
        try
        {
            var items = new List<string>();
            foreach (var item in arrayElement.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var itemType) && itemType.GetString() == "object")
                {
                    if (item.TryGetProperty("valueObject", out var valueObject))
                    {
                        items.Add(ExtractObjectValue(valueObject));
                    }
                }
                else
                {
                    items.Add(ExtractFieldValue(item));
                }
            }
            return items.Count > 0 ? string.Join("; ", items) : "Empty Array";
        }
        catch
        {
            return "Array Parse Error";
        }
    }

    /// <summary>
    /// Extracts a readable value from an object field
    /// </summary>
    private static string ExtractObjectValue(JsonElement objectElement)
    {
        try
        {
            var properties = new List<string>();
            foreach (var property in objectElement.EnumerateObject())
            {
                var value = ExtractFieldValue(property.Value);
                properties.Add($"{property.Name}: {value}");
            }
            return properties.Count > 0 ? $"[{string.Join(", ", properties)}]" : "Empty Object";
        }
        catch
        {
            return "Object Parse Error";
        }
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

    private static async Task RunCreateAnalyzerAsync(IServiceProvider serviceProvider, string analyzername, string analyzerFile)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();
        
        logger.LogInformation("📝 Creating analyzer from JSON files...");
        logger.LogInformation("");

        try
        {
            var jsonContent = await SampleAnalyzers.LoadAnalyzerJsonAsync(analyzerFile);
            
            // Validate the JSON
            if (!SampleAnalyzers.ValidateAnalyzerJson(jsonContent))
            {
                logger.LogError("❌ Invalid analyzer JSON format in file: {FileName}", analyzername);
                return;
            }

            logger.LogInformation("✅ JSON validation passed");
            logger.LogInformation("🚀 Creating analyzer: {AnalyzerName}", analyzername);

            // Create the analyzer using the Content Understanding service
            var result = await contentUnderstandingService.CreateOrUpdateAnalyzerAsync(analyzername, jsonContent);
            
            logger.LogInformation("✅ Successfully created analyzer: {AnalyzerName}", analyzername);
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

    private static async Task RunTestAnalysisAsync(IServiceProvider serviceProvider, string analyzerName = "", string documentFile = "")
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();

        logger.LogInformation("🔍 Test document analysis mode...");
        logger.LogInformation("");

        try
        {
            // Determine which document to analyze
            var projectRoot = Directory.GetCurrentDirectory();
            var sampleDocumentsPath = Path.Combine(projectRoot, "Data", "SampleDocuments");
            
            string targetDocumentPath;
            string documentFileName;
            
            if (!string.IsNullOrEmpty(documentFile))
            {
                // Check if it's a full path or just filename
                if (Path.IsPathFullyQualified(documentFile))
                {
                    targetDocumentPath = documentFile;
                    documentFileName = Path.GetFileName(documentFile);
                }
                else
                {
                    targetDocumentPath = Path.Combine(sampleDocumentsPath, documentFile);
                    documentFileName = documentFile;
                }
                
                if (!File.Exists(targetDocumentPath))
                {
                    logger.LogError("❌ Specified document not found: {FilePath}", targetDocumentPath);
                    
                    // Show available documents
                    if (Directory.Exists(sampleDocumentsPath))
                    {
                        var availableDocs = Directory.GetFiles(sampleDocumentsPath, "*.*")
                            .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".pdf" or ".png" or ".jpg" or ".jpeg" or ".tiff" or ".bmp")
                            .Select(Path.GetFileName)
                            .ToArray();
                        
                        if (availableDocs.Any())
                        {
                            logger.LogInformation("📄 Available documents in Data/SampleDocuments:");
                            foreach (var doc in availableDocs)
                            {
                                logger.LogInformation("   • {DocumentName}", doc);
                            }
                        }
                    }
                    return;
                }
            }
            else
            {
                // Default to receipt1.pdf
                targetDocumentPath = Path.Combine(sampleDocumentsPath, "receipt1.pdf");
                documentFileName = "receipt1.pdf";
                
                if (!File.Exists(targetDocumentPath))
                {
                    logger.LogError("❌ Default document not found: {FilePath}", targetDocumentPath);
                    
                    // Show available documents
                    if (Directory.Exists(sampleDocumentsPath))
                    {
                        var availableDocs = Directory.GetFiles(sampleDocumentsPath, "*.*")
                            .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".pdf" or ".png" or ".jpg" or ".jpeg" or ".tiff" or ".bmp")
                            .Select(Path.GetFileName)
                            .ToArray();
                        
                        if (availableDocs.Any())
                        {
                            logger.LogInformation("📄 Available documents in Data/SampleDocuments:");
                            foreach (var doc in availableDocs)
                            {
                                logger.LogInformation("   • {DocumentName}", doc);
                            }
                            logger.LogInformation("");
                            logger.LogInformation("� Use --document <filename> to specify a different document");
                        }
                    }
                    return;
                }
            }

            logger.LogInformation("📄 Using document: {FileName}", documentFileName);
            logger.LogInformation("📁 Document path: {FilePath}", targetDocumentPath);

            // Read the document file
            var documentData = await File.ReadAllBytesAsync(targetDocumentPath);
            logger.LogInformation("📊 Document size: {Size} bytes", documentData.Length);

            // Determine which analyzer to use
            string targetAnalyzer;
            if (!string.IsNullOrEmpty(analyzerName))
            {
                targetAnalyzer = analyzerName;
                logger.LogInformation("🎯 Using specified analyzer: {AnalyzerName}", analyzerName);
            }
            else
            {
                // Default to receipt analyzer, but check if it exists
                try
                {
                    var listAnalyzersResult = await contentUnderstandingService.ListAnalyzersAsync();
                    var doc = JsonDocument.Parse(listAnalyzersResult);
                    var analyzers = doc.RootElement.GetProperty("value");
                    
                    var availableAnalyzerNames = new List<string>();
                    foreach (var analyzer in analyzers.EnumerateArray())
                    {
                        availableAnalyzerNames.Add(analyzer.GetProperty("id").GetString() ?? "");
                    }
                    
                    if (availableAnalyzerNames.Contains("receipt"))
                    {
                        targetAnalyzer = "receipt";
                        logger.LogInformation("🔧 Using default analyzer: receipt");
                    }
                    else if (availableAnalyzerNames.Any())
                    {
                        targetAnalyzer = availableAnalyzerNames.First();
                        logger.LogInformation("🔧 Using first available analyzer: {AnalyzerName}", targetAnalyzer);
                        logger.LogInformation("💡 Available analyzers: {Analyzers}", string.Join(", ", availableAnalyzerNames));
                    }
                    else
                    {
                        logger.LogError("❌ No analyzers found. Please create an analyzer first with --mode create-analyzer");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("❌ Failed to list analyzers: {Message}", ex.Message);
                    logger.LogInformation("💡 Falling back to default analyzer: receipt");
                    targetAnalyzer = "receipt";
                }
            }
            
            logger.LogInformation("🧠 Analyzing document with analyzer: {AnalyzerName}", targetAnalyzer);

            // Get the content type based on file extension
            var fileExtension = Path.GetExtension(targetDocumentPath).ToLowerInvariant();
            var contentType = fileExtension switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".tiff" or ".tif" => "image/tiff",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };

            // Submit document for analysis
            var analysisResult = await contentUnderstandingService.AnalyzeDocumentAsync(
                targetAnalyzer, 
                documentData, 
                contentType);

            logger.LogInformation("✅ Document analysis submitted successfully!");
            logger.LogInformation("📊 Analysis Result: {Result}", analysisResult.responseContent);

            // Check if we got an operation location URL for polling
            if (!string.IsNullOrEmpty(analysisResult.operationLocation))
            {
                logger.LogInformation("🔄 Operation Location: {OperationLocation}", analysisResult.operationLocation);
                logger.LogInformation("⏳ Polling for analysis results (up to 20 minutes)...");
                
                // Poll for results using the operation location URL with extended timeout
                var startTime = DateTime.UtcNow;
                var maxDuration = TimeSpan.FromMinutes(20); // Extended to 20 minutes
                int attempt = 1;
                
                while (DateTime.UtcNow - startTime < maxDuration)
                {
                    // Use progressive backoff: start with 10s, then increase gradually up to 30s max
                    int delaySeconds = Math.Min(10 + (attempt - 1) * 5, 30);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    
                    logger.LogInformation("🔄 Polling attempt {Attempt} (elapsed: {Elapsed:mm\\:ss}, next check in {Delay}s)...", 
                        attempt, DateTime.UtcNow - startTime, delaySeconds);
                    
                    try
                    {
                        var resultResponse = await contentUnderstandingService.GetAnalysisResultByLocationAsync(analysisResult.operationLocation);
                        var resultDoc = System.Text.Json.JsonDocument.Parse(resultResponse);
                        var currentStatus = resultDoc.RootElement.GetProperty("status").GetString();
                        
                        logger.LogInformation("📊 Status: {Status}", currentStatus);
                        
                        if (currentStatus == "Succeeded")
                        {
                            logger.LogInformation("🎉 Analysis completed successfully in {Elapsed:mm\\:ss}!", 
                                DateTime.UtcNow - startTime);
                            
                            // Display results summary instead of full JSON
                            logger.LogInformation("📋 Analysis Results Summary:");
                            try
                            {
                                var analysisDoc = System.Text.Json.JsonDocument.Parse(resultResponse);
                                
                                // The structure is: result -> contents[0] -> fields
                                if (analysisDoc.RootElement.TryGetProperty("result", out var result) &&
                                    result.TryGetProperty("contents", out var contents) &&
                                    contents.GetArrayLength() > 0)
                                {
                                    var firstContent = contents[0];
                                    if (firstContent.TryGetProperty("fields", out var fields))
                                    {
                                        foreach (var field in fields.EnumerateObject())
                                        {
                                            var fieldValue = ExtractFieldValue(field.Value);
                                            logger.LogInformation("  • {FieldName}: {FieldValue}", field.Name, fieldValue);
                                        }
                                    }
                                }
                                else
                                {
                                    logger.LogInformation("  ✅ Document processed successfully, but no specific fields were extracted");
                                    logger.LogInformation("  💡 This might be normal depending on the analyzer schema");
                                }
                                
                                // Extract operation ID for reference
                                var operationId = analysisResult.operationLocation.Split('/').LastOrDefault();
                                logger.LogInformation("🆔 Operation ID: {OperationId}", operationId);
                                
                                // Export results to files
                                await ExportAnalysisResultsAsync(resultResponse, operationId ?? "unknown", documentFileName, logger);
                            }
                            catch (Exception parseEx)
                            {
                                logger.LogWarning("⚠️ Could not parse results summary: {Message}", parseEx.Message);
                                logger.LogDebug(parseEx, "Full parsing exception");
                                logger.LogInformation("📋 Operation completed successfully - raw results available via API");
                                
                                // Still try to export raw results
                                var operationId = analysisResult.operationLocation.Split('/').LastOrDefault();
                                await ExportAnalysisResultsAsync(resultResponse, operationId ?? "unknown", documentFileName, logger);
                            }
                            break;
                        }
                        else if (currentStatus == "Failed")
                        {
                            logger.LogError("❌ Analysis failed after {Elapsed:mm\\:ss}", DateTime.UtcNow - startTime);
                            
                            // Try to extract error details
                            try
                            {
                                if (resultDoc.RootElement.TryGetProperty("error", out var error))
                                {
                                    var errorCode = error.TryGetProperty("code", out var code) ? code.GetString() : "Unknown";
                                    var errorMessage = error.TryGetProperty("message", out var message) ? message.GetString() : "No details available";
                                    logger.LogError("❌ Error Code: {ErrorCode}", errorCode);
                                    logger.LogError("❌ Error Message: {ErrorMessage}", errorMessage);
                                }
                                else
                                {
                                    logger.LogInformation("📋 Full Error Response: {Results}", resultResponse);
                                }
                            }
                            catch
                            {
                                logger.LogInformation("📋 Error Details: {Results}", resultResponse);
                            }
                            break;
                        }
                        else if (currentStatus == "Running" || currentStatus == "NotStarted")
                        {
                            logger.LogInformation("⏳ Analysis still in progress...");
                        }
                        else
                        {
                            logger.LogWarning("⚠️ Unknown status: {Status} - continuing to poll", currentStatus);
                        }
                    }
                    catch (Exception pollEx)
                    {
                        logger.LogError("❌ Error during polling attempt {Attempt}: {Message}", attempt, pollEx.Message);
                        logger.LogDebug(pollEx, "Polling exception details");
                        
                        // Don't break on individual polling errors, continue trying
                        logger.LogInformation("🔄 Continuing to poll despite error...");
                    }
                    
                    attempt++;
                }
                
                // Check if we timed out
                if (DateTime.UtcNow - startTime >= maxDuration)
                {
                    var operationId = analysisResult.operationLocation.Split('/').LastOrDefault();
                    logger.LogWarning("⏰ Polling timeout after 20 minutes. Operation may still be processing.");
                    logger.LogInformation("💡 Operation ID: {OperationId}", operationId);
                    logger.LogInformation("💡 You can check the status later using:");
                    logger.LogInformation("    dotnet run -- --mode check-operation --operation-id {OperationId}", operationId);
                    logger.LogInformation("💡 Or check the Azure portal for completion status.");
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
    
    /// <summary>
    /// Checks the status of a specific Content Understanding operation
    /// </summary>
    private static async Task RunCheckOperationAsync(IServiceProvider serviceProvider, string operationId = "")
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();

        try
        {
            if (string.IsNullOrEmpty(operationId))
            {
                logger.LogError("❌ Operation ID is required for check-operation mode");
                logger.LogInformation("💡 Usage: dotnet run -- --mode check-operation --operation-id <operation-id>");
                logger.LogInformation("💡 Example: dotnet run -- --mode check-operation --operation-id 069e39de-5132-425d-87b7-9f84cd4317f5");
                return;
            }

            logger.LogInformation("🔍 Checking operation: {OperationId}", operationId);

            // Try to get the operation result directly
            var resultResponse = await contentUnderstandingService.GetAnalysisResultAsync(operationId);
            var resultDoc = System.Text.Json.JsonDocument.Parse(resultResponse);
            var currentStatus = resultDoc.RootElement.GetProperty("status").GetString();

            logger.LogInformation("📊 Operation Status: {Status}", currentStatus);

            switch (currentStatus)
            {
                case "Succeeded":
                    logger.LogInformation("🎉 Operation completed successfully!");
                    
                    // Display results summary instead of full JSON
                    logger.LogInformation("📋 Analysis Results Summary:");
                    try
                    {
                        var analysisDoc = System.Text.Json.JsonDocument.Parse(resultResponse);
                        if (analysisDoc.RootElement.TryGetProperty("analyzeResult", out var analyzeResult))
                        {
                            if (analyzeResult.TryGetProperty("documents", out var documents) && documents.GetArrayLength() > 0)
                            {
                                var document = documents[0];
                                if (document.TryGetProperty("fields", out var fields))
                                {
                                    foreach (var field in fields.EnumerateObject())
                                    {
                                        var fieldValue = field.Value.TryGetProperty("content", out var content) ? content.GetString() : field.Value.GetString();
                                        logger.LogInformation("  • {FieldName}: {FieldValue}", field.Name, fieldValue);
                                    }
                                }
                            }
                        }
                        
                        // Export results for check-operation mode too
                        await ExportAnalysisResultsAsync(resultResponse, operationId, $"operation_{operationId}", logger);
                    }
                    catch
                    {
                        // Fallback to showing key information instead of full JSON
                        logger.LogInformation("📋 Operation completed - check Azure portal for detailed results");
                        
                        // Still try to export raw results
                        await ExportAnalysisResultsAsync(resultResponse, operationId, $"operation_{operationId}", logger);
                    }
                    break;

                case "Failed":
                    logger.LogError("❌ Operation failed");
                    logger.LogInformation("📋 Error Details: {Results}", resultResponse);
                    break;

                case "Running":
                case "NotStarted":
                    logger.LogInformation("⏳ Operation is still in progress...");
                    logger.LogInformation("💡 Try checking again in a few minutes");
                    break;

                default:
                    logger.LogWarning("⚠️ Unknown status: {Status}", currentStatus);
                    logger.LogInformation("📋 Full Response: {Results}", resultResponse);
                    break;
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            logger.LogError("❌ Operation not found: {OperationId}", operationId);
            logger.LogInformation("💡 The operation may have expired or the ID is incorrect");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to check operation: {OperationId}", operationId);
        }
    }
    
    /// <summary>
    /// Displays help information about available commands and usage
    /// </summary>
    private static void DisplayHelpInformation(ILogger logger)
    {
        logger.LogInformation("Azure Content Understanding C# Sample Application");
        logger.LogInformation("==================================================");
        logger.LogInformation("");
        logger.LogInformation("USAGE:");
        logger.LogInformation("  dotnet run [-- --mode <mode>] [options]");
        logger.LogInformation("");
        logger.LogInformation("MODES:");
        logger.LogInformation("  help, --help, -h       Show this help information");
        logger.LogInformation("  health, healthcheck     Run comprehensive health check of Azure resources");
        logger.LogInformation("  analyzers, list         List all available analyzers in the service");
        logger.LogInformation("  create-analyzer, create Create analyzer from JSON schema files");
        logger.LogInformation("  test-analysis, analyze  Analyze documents with specified analyzer");
        logger.LogInformation("  check-operation         Check the status of a specific operation");
        logger.LogInformation("  interactive            Interactive mode with menu (default)");
        logger.LogInformation("");
        logger.LogInformation("OPTIONS:");
        logger.LogInformation("  --analyzer-file <file>  Specify analyzer JSON file for create-analyzer mode");
        logger.LogInformation("                         (looks in Data folder, supports partial names)");
        logger.LogInformation("  --analyzer <name>      Specify analyzer name for analysis mode");
        logger.LogInformation("  --document <file>      Specify document file for analysis mode");
        logger.LogInformation("                         (looks in Data/SampleDocuments or absolute path)");
        logger.LogInformation("  --operation-id <id>    Specify operation ID for check-operation mode");
        logger.LogInformation("");
        logger.LogInformation("EXAMPLES:");
        logger.LogInformation("  dotnet run                                          # Interactive mode");
        logger.LogInformation("  dotnet run -- --mode health                         # Health check only");
        logger.LogInformation("  dotnet run -- --mode create-analyzer                # Create default analyzer");
        logger.LogInformation("  dotnet run -- --mode create-analyzer --analyzer-file receipt.json");
        logger.LogInformation("  dotnet run -- --mode test-analysis                  # Analyze with defaults");
        logger.LogInformation("  dotnet run -- --mode analyze --analyzer receipt --document invoice1.pdf");
        logger.LogInformation("  dotnet run -- --mode analyze --document sample.png  # Auto-detect analyzer");
        logger.LogInformation("  dotnet run -- --mode check-operation --operation-id 069e39de-5132-425d-87b7-9f84cd4317f5");
        logger.LogInformation("");
        logger.LogInformation("FEATURES:");
        logger.LogInformation("  ✅ Complete Azure Content Understanding API integration");
        logger.LogInformation("  ✅ Health checks for all Azure resources");
        logger.LogInformation("  ✅ JSON-based analyzer schema management"); 
        logger.LogInformation("  ✅ End-to-end document analysis pipeline");
        logger.LogInformation("  ✅ Real-time polling and result formatting");
        logger.LogInformation("  ✅ Results export to JSON and formatted text files");
        logger.LogInformation("  ✅ Multi-format support (PDF, PNG, JPG, TIFF, BMP)");
        logger.LogInformation("  ✅ Parameterized operations with intelligent defaults");
        logger.LogInformation("");
        logger.LogInformation("OUTPUT:");
        logger.LogInformation("  Analysis results are saved to the 'Output' folder:");
        logger.LogInformation("  • Raw JSON results: *_results.json");
        logger.LogInformation("  • Formatted results: *_formatted.txt");
        logger.LogInformation("");
        logger.LogInformation("For more information, run in interactive mode or check the documentation.");
    }

    /// <summary>
    /// Exports analysis results to JSON and formatted text files in the Output directory
    /// </summary>
    private static async Task ExportAnalysisResultsAsync(string resultResponse, string operationId, string documentName, ILogger logger)
    {
        try
        {
            // Create Output directory if it doesn't exist
            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Output");
            Directory.CreateDirectory(outputDir);

            // Generate timestamp for unique filenames
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var safeDocumentName = Path.GetFileNameWithoutExtension(documentName);
            
            // Extract just the operation ID part (before any query parameters or additional info)
            var cleanOperationId = operationId.Split('?')[0]; // Remove query parameters first
            var safeOperationId = cleanOperationId.Contains('_') 
                ? cleanOperationId.Split('_')[0] 
                : cleanOperationId;

            // Raw JSON results file
            var jsonFileName = $"{safeDocumentName}_{safeOperationId}_{timestamp}_results.json";
            var jsonFilePath = Path.Combine(outputDir, jsonFileName);
            
            // Pretty-print the JSON
            var jsonDocument = JsonDocument.Parse(resultResponse);
            var prettyJson = JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(jsonFilePath, prettyJson);

            // Formatted text results file
            var txtFileName = $"{safeDocumentName}_{safeOperationId}_{timestamp}_formatted.txt";
            var txtFilePath = Path.Combine(outputDir, txtFileName);
            
            var formattedContent = await CreateFormattedResultsAsync(resultResponse, documentName, operationId);
            await File.WriteAllTextAsync(txtFilePath, formattedContent);

            logger.LogInformation("💾 Results exported to Output folder:");
            logger.LogInformation("   📄 Raw JSON: {JsonFile}", jsonFileName);
            logger.LogInformation("   📝 Formatted: {TxtFile}", txtFileName);
            logger.LogInformation("   📁 Location: {OutputDir}", outputDir);
        }
        catch (Exception ex)
        {
            logger.LogWarning("⚠️ Failed to export results to files: {Message}", ex.Message);
            logger.LogDebug(ex, "Export exception details");
        }
    }

    /// <summary>
    /// Creates a human-readable formatted version of the analysis results
    /// </summary>
    private static Task<string> CreateFormattedResultsAsync(string resultResponse, string documentName, string operationId)
    {
        var content = new List<string>
        {
            "Azure Content Understanding - Analysis Results",
            "=" + new string('=', 47),
            "",
            $"Document: {documentName}",
            $"Operation ID: {operationId}",
            $"Processed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            "",
            "EXTRACTED FIELDS:",
            "-".PadRight(50, '-'),
            ""
        };

        try
        {
            var analysisDoc = JsonDocument.Parse(resultResponse);
            
            // The structure is: result -> contents[0] -> fields
            if (analysisDoc.RootElement.TryGetProperty("result", out var result) &&
                result.TryGetProperty("contents", out var contents) &&
                contents.GetArrayLength() > 0)
            {
                var firstContent = contents[0];
                if (firstContent.TryGetProperty("fields", out var fields))
                {
                    foreach (var field in fields.EnumerateObject())
                    {
                        var fieldValue = ExtractFieldValue(field.Value);
                        content.Add($"• {field.Name}:");
                        content.Add($"  {fieldValue}");
                        content.Add("");
                    }
                }
                else
                {
                    content.Add("No specific fields were extracted from this document.");
                    content.Add("This might be normal depending on the analyzer schema.");
                }
            }
            else
            {
                content.Add("No analysis results found in the response.");
            }
        }
        catch (Exception ex)
        {
            content.Add($"Error parsing results: {ex.Message}");
        }

        content.Add("");
        content.Add("RAW JSON RESPONSE:");
        content.Add("-".PadRight(50, '-'));
        
        try
        {
            var jsonDocument = JsonDocument.Parse(resultResponse);
            var prettyJson = JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions { WriteIndented = true });
            content.Add(prettyJson);
        }
        catch
        {
            content.Add(resultResponse);
        }

        return Task.FromResult(string.Join(Environment.NewLine, content));
    }
}
