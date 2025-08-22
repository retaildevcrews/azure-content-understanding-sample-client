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
using ContentUnderstanding.Client.Services;
using ContentUnderstanding.Client.Data;

namespace ContentUnderstanding.Client;

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
            var classifierFile = GetArgumentValue(args, "--classifier-file", "");
            var classifierName = GetArgumentValue(args, "--classifier", "");
            var text = GetArgumentValue(args, "--text", "");
            
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
                    if (!string.IsNullOrWhiteSpace(analyzerName) || !string.IsNullOrWhiteSpace(documentFile))
                    {
                        await RunAnalysisAsync(serviceProvider, analyzerName, documentFile);
                    }
                    else
                    {
                        // No parameters provided: run the default test (receipt analyzer + sample file)
                        await RunTestAnalysisAsync(serviceProvider);
                    }
                    break;
                case "check-operation":
                case "operation":
                    await RunCheckOperationAsync(serviceProvider, operationId);
                    break;
                case "classifiers":
                case "list-classifiers":
                    await RunListClassifiersAsync(serviceProvider);
                    break;
                case "create-classifier":
                case "create-clf":
                    await RunCreateClassifierAsync(serviceProvider, classifierName, classifierFile);
                    break;
                case "classify":
                    await RunClassifyAsync(serviceProvider, classifierName, documentFile, text);
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
        logger.LogInformation("  --mode classifiers      : List all classifiers");
        logger.LogInformation("  --mode create-classifier: Create classifier from JSON");
        logger.LogInformation("  --mode classify         : Classify text or documents");
        logger.LogInformation("");
        
        // TODO: Implement interactive menu for Content Understanding operations
        logger.LogInformation("🚧 Content Understanding operations ready for testing...");
        logger.LogInformation("");
        logger.LogInformation("Next steps:");
        logger.LogInformation("1. Run health check: dotnet run -- --mode health");
        logger.LogInformation("2. List analyzers: dotnet run -- --mode analyzers");
        logger.LogInformation("3. Create sample analyzer: dotnet run -- --mode create-analyzer");
        logger.LogInformation("4. List classifiers: dotnet run -- --mode classifiers");
        logger.LogInformation("5. Create classifier: dotnet run -- --mode create-classifier --classifier-file <file> --classifier <name>");
        logger.LogInformation("6. Classify: dotnet run -- --mode classify --classifier <name> --text \"...\" | --document <file>");
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
            try
            {
                using var doc = JsonDocument.Parse(result);
                var pretty = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
                logger.LogInformation("{Result}", pretty);
            }
            catch
            {
                logger.LogInformation("{Result}", result);
            }
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
        logger.LogInformation("📝 Creating analyzer from JSON...");
        logger.LogInformation("");

        try
        {
            if (string.IsNullOrWhiteSpace(analyzername))
            {
                logger.LogError("❌ Please specify an analyzer name with --analyzer <name>");
                return;
            }

            if (string.IsNullOrWhiteSpace(analyzerFile))
            {
                logger.LogError("❌ Please specify an analyzer JSON file with --analyzer-file <file>");
                return;
            }

            var jsonContent = await SampleAnalyzers.LoadAnalyzerJsonAsync(analyzerFile);

            if (!SampleAnalyzers.ValidateAnalyzerJson(jsonContent))
            {
                logger.LogError("❌ Invalid analyzer JSON format in file: {FileName}", analyzerFile);
                return;
            }

            logger.LogInformation("✅ JSON validation passed");
            logger.LogInformation("🚀 Creating analyzer: {AnalyzerName}", analyzername);

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

    // Parameter-driven analysis (requires analyzer and document)
    private static async Task RunAnalysisAsync(IServiceProvider serviceProvider, string analyzerName, string documentFile)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();

        logger.LogInformation("🔍 Analyze mode...");
        logger.LogInformation("");

        if (string.IsNullOrWhiteSpace(analyzerName))
        {
            logger.LogError("❌ Please specify an analyzer with --analyzer <name>");
            return;
        }
        if (string.IsNullOrWhiteSpace(documentFile))
        {
            logger.LogError("❌ Please specify a document with --document <file>");
            return;
        }

        try
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var sampleDocumentsPath = Path.Combine(projectRoot, "Data", "SampleDocuments");

            string targetDocumentPath;
            string documentFileName;

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
                logger.LogError("❌ Document not found: {Path}", targetDocumentPath);
                return;
            }

            var documentData = await File.ReadAllBytesAsync(targetDocumentPath);
            logger.LogInformation("🧠 Using analyzer: {Analyzer}", analyzerName);
            logger.LogInformation("📄 Document: {File}", documentFileName);

            var ext = Path.GetExtension(targetDocumentPath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".tiff" or ".tif" => "image/tiff",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };

            var analysisResult = await contentUnderstandingService.AnalyzeDocumentAsync(
                analyzerName,
                documentData,
                contentType);

            await HandleAnalysisResultAsync(contentUnderstandingService, analysisResult, logger, documentFileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to analyze");
        }
    }

    // Existing: Default test analysis that picks a known analyzer and sample file when none are provided
    private static async Task RunTestAnalysisAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();

        logger.LogInformation("🔍 Test document analysis mode (defaults)...");
        logger.LogInformation("");

        try
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var sampleDocumentsPath = Path.Combine(projectRoot, "Data", "SampleDocuments");
            var targetDocumentPath = Path.Combine(sampleDocumentsPath, "receipt1.pdf");
            var documentFileName = "receipt1.pdf";

            if (!File.Exists(targetDocumentPath))
            {
                logger.LogError("❌ Default document not found: {FilePath}", targetDocumentPath);
                return;
            }

            logger.LogInformation("📄 Using document: {FileName}", documentFileName);
            logger.LogInformation("📁 Document path: {FilePath}", targetDocumentPath);

            var documentData = await File.ReadAllBytesAsync(targetDocumentPath);
            logger.LogInformation("📊 Document size: {Size} bytes", documentData.Length);

            // Ensure a default analyzer exists, prefer 'receipt'
            string targetAnalyzer = "receipt";
            try
            {
                var listAnalyzersResult = await contentUnderstandingService.ListAnalyzersAsync();
                using var doc = JsonDocument.Parse(listAnalyzersResult);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var analyzer in doc.RootElement.EnumerateArray())
                    {
                        if (analyzer.TryGetProperty("analyzerId", out var aId) && !string.IsNullOrWhiteSpace(aId.GetString()))
                            names.Add(aId.GetString()!);
                        else if (analyzer.TryGetProperty("id", out var legacyId) && !string.IsNullOrWhiteSpace(legacyId.GetString()))
                            names.Add(legacyId.GetString()!);
                    }
                    if (!names.Contains("receipt") && names.Count > 0)
                        targetAnalyzer = names.First();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("⚠️ Could not list analyzers, defaulting to 'receipt': {Message}", ex.Message);
            }

            logger.LogInformation("🎯 Using analyzer: {AnalyzerName}", targetAnalyzer);

            var contentType = "application/pdf";
            var analysisResult = await contentUnderstandingService.AnalyzeDocumentAsync(
                targetAnalyzer,
                documentData,
                contentType);

            await HandleAnalysisResultAsync(contentUnderstandingService, analysisResult, logger, documentFileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to run default test analysis");
        }
    }
    
    // Shared result handling for analysis operations
    private static async Task HandleAnalysisResultAsync(
        ContentUnderstandingService contentUnderstandingService,
        (string responseContent, string operationLocation) analysisResult,
        ILogger logger,
        string documentFileName)
    {
        logger.LogInformation("✅ Analysis submitted successfully!");
        logger.LogInformation("📊 Initial Response: {Result}", analysisResult.responseContent);

        if (!string.IsNullOrEmpty(analysisResult.operationLocation))
        {
            logger.LogInformation("🔄 Operation Location: {OperationLocation}", analysisResult.operationLocation);
            logger.LogInformation("⏳ Polling for analysis results (up to 20 minutes)...");

            try
            {
                var resultDoc = await contentUnderstandingService.PollResultAsync(
                    analysisResult.operationLocation,
                    timeoutSeconds: 1200,
                    pollingIntervalSeconds: 5);

                var resultResponse = resultDoc.RootElement.GetRawText();

                // Display results summary instead of full JSON
                logger.LogInformation("📋 Analysis Results Summary:");
                try
                {
                    // The structure is: result -> contents[0] -> fields
                    if (resultDoc.RootElement.TryGetProperty("result", out var result) &&
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
            }
            catch (TimeoutException tex)
            {
                var operationId = analysisResult.operationLocation.Split('/').LastOrDefault();
                logger.LogWarning("⏰ {Message}", tex.Message);
                logger.LogInformation("🆔 Operation ID: {OperationId}", operationId);
                logger.LogInformation("💡 You can check the status later using:");
                logger.LogInformation("    dotnet run -- --mode check-operation --operation-id {OperationId}", operationId);
                logger.LogInformation("💡 Or check the Azure portal for completion status.");
            }
            catch (InvalidOperationException ioex)
            {
                logger.LogError("❌ Analysis failed: {Message}", ioex.Message);
            }
        }
        else
        {
            logger.LogWarning("⚠️ No Operation-Location header received. Cannot poll for results.");
            // Fallback: try to parse the operation ID from the response content
            try
            {
                using var analysisResponse = JsonDocument.Parse(analysisResult.responseContent);
                if (analysisResponse.RootElement.TryGetProperty("id", out var idProp))
                {
                    var operationId = idProp.GetString();
                    logger.LogInformation("🔄 Fallback: Found Operation ID in response: {OperationId}", operationId);
                }
            }
            catch (Exception parseEx)
            {
                logger.LogDebug(parseEx, "Could not parse operation ID from response");
            }
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
            var resultResponse = await contentUnderstandingService.GetOperationResultAsync(operationId);
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
    
    private static async Task RunListClassifiersAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();

        logger.LogInformation("📋 Listing all classifiers...");
        logger.LogInformation("");

        try
        {
            var result = await contentUnderstandingService.ListClassifiersAsync();
            logger.LogInformation("✅ Classifiers retrieved successfully:");
            logger.LogInformation("{Result}", result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to list classifiers");
            Environment.Exit(1);
        }
    }

    // NEW: Create classifier from JSON
    private static async Task RunCreateClassifierAsync(IServiceProvider serviceProvider, string classifierName, string classifierFile)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();
        
        logger.LogInformation("📝 Creating classifier from JSON files...");
        logger.LogInformation("");

        try
        {
            if (string.IsNullOrWhiteSpace(classifierName))
            {
                logger.LogError("❌ Please specify a classifier name with --classifier <name>");
                return;
            }

            if (string.IsNullOrWhiteSpace(classifierFile))
            {
                logger.LogError("❌ Please specify a classifier JSON file with --classifier-file <file>");
                return;
            }

            var jsonContent = await SampleClassifiers.LoadClassifierJsonAsync(classifierFile);

            if (!SampleClassifiers.ValidateClassifierJson(jsonContent))
            {
                logger.LogError("❌ Invalid classifier JSON format in file: {FileName}", classifierFile);
                return;
            }

            logger.LogInformation("✅ JSON validation passed");
            logger.LogInformation("🚀 Creating classifier: {ClassifierName}", classifierName);

            var result = await contentUnderstandingService.CreateOrUpdateClassifierAsync(classifierName, jsonContent);

            logger.LogInformation("✅ Successfully created classifier: {ClassifierName}", classifierName);
            logger.LogDebug("API Response: {Result}", result);
        }
        catch (FileNotFoundException ex)
        {
            logger.LogError("❌ Classifier JSON file not found: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to create classifier");
        }
    }

    // NEW: Classify content (file or text)
    private static async Task RunClassifyAsync(IServiceProvider serviceProvider, string classifierName, string documentFile, string text = "")
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();

        logger.LogInformation("🔎 Classify mode...");
        logger.LogInformation("");

        try
        {
            if (string.IsNullOrWhiteSpace(classifierName))
            {
                logger.LogError("❌ Please specify a classifier with --classifier <name>");
                return;
            }

            // If text provided, classify text
            if (!string.IsNullOrWhiteSpace(text))
            {
                logger.LogInformation("📝 Classifying text using classifier: {Classifier}", classifierName);

                var classifyResult = await contentUnderstandingService.ClassifyTextAsync(classifierName, text);
                await HandleAnalysisResultAsync(contentUnderstandingService, classifyResult, logger, "text");
                return;
            }

            // Otherwise, require documentFile
            if (string.IsNullOrWhiteSpace(documentFile))
            {
                logger.LogError("❌ Provide --text \"...\" or --document <file>");
                return;
            }

            var projectRoot = Directory.GetCurrentDirectory();
            var sampleDocumentsPath = Path.Combine(projectRoot, "Data", "SampleDocuments");

            string targetDocumentPath;
            string documentFileName;

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
                logger.LogError("❌ Document not found: {Path}", targetDocumentPath);
                return;
            }

            var bytes = await File.ReadAllBytesAsync(targetDocumentPath);
            var ext = Path.GetExtension(targetDocumentPath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".tiff" or ".tif" => "image/tiff",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };

            logger.LogInformation("🧠 Using classifier: {Classifier}", classifierName);
            logger.LogInformation("📄 Document: {File}", documentFileName);

            var classifyResultFile = await contentUnderstandingService.ClassifyAsync(classifierName, bytes, contentType);
            await HandleAnalysisResultAsync(contentUnderstandingService, classifyResultFile, logger, documentFileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to classify");
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
        logger.LogInformation("  classifiers             List all classifiers");
        logger.LogInformation("  create-classifier       Create classifier from JSON schema");
        logger.LogInformation("  classify                Classify text or a document with a classifier");
        logger.LogInformation("  interactive            Interactive mode with menu (default)");
        logger.LogInformation("");
        logger.LogInformation("OPTIONS:");
        logger.LogInformation("  --analyzer-file <file>  Specify analyzer JSON file for create-analyzer mode");
        logger.LogInformation("                         (looks in Data folder, supports partial names)");
        logger.LogInformation("  --analyzer <name>      Specify analyzer name for analysis mode");
        logger.LogInformation("  --document <file>      Specify document file for analysis or classification");
        logger.LogInformation("                         (looks in Data/SampleDocuments or absolute path)");
        logger.LogInformation("  --operation-id <id>    Specify operation ID for check-operation mode");
        logger.LogInformation("  --classifier <name>    Specify classifier name for classification");
        logger.LogInformation("  --classifier-file <f>  Specify classifier JSON file for create-classifier");
        logger.LogInformation("  --text \"...\"          Provide inline text to classify");
        logger.LogInformation("");
        logger.LogInformation("EXAMPLES:");
        logger.LogInformation("  dotnet run                                          # Interactive mode");
        logger.LogInformation("  dotnet run -- --mode health                         # Health check only");
        logger.LogInformation("  dotnet run -- --mode analyzers                      # List analyzers");
        logger.LogInformation("  dotnet run -- --mode create-analyzer --analyzer-file receipt.json --analyzer receipt");
        logger.LogInformation("  dotnet run -- --mode analyze --analyzer receipt --document receipt1.pdf");
        logger.LogInformation("  dotnet run -- --mode check-operation --operation-id 069e39de-5132-425d-87b7-9f84cd4317f5");
        logger.LogInformation("  dotnet run -- --mode classifiers                    # List classifiers");
        logger.LogInformation("  dotnet run -- --mode create-classifier --classifier-file product-categories.json --classifier products");
        logger.LogInformation("  dotnet run -- --mode classify --classifier products --text \"laptop backpack\"");
        logger.LogInformation("  dotnet run -- --mode classify --classifier products --document sample.png");
        logger.LogInformation("");
        logger.LogInformation("FEATURES:");
        logger.LogInformation("  ✅ Complete Azure Content Understanding API integration");
        logger.LogInformation("  ✅ Health checks for all Azure resources");
        logger.LogInformation("  ✅ JSON-based analyzer/classifier schema management"); 
        logger.LogInformation("  ✅ End-to-end analysis and classification pipelines");
        logger.LogInformation("  ✅ Real-time polling and result formatting");
        logger.LogInformation("  ✅ Results export to JSON and formatted text files");
        logger.LogInformation("  ✅ Multi-format support (PDF, PNG, JPG, TIFF, BMP)");
        logger.LogInformation("  ✅ Parameterized operations with intelligent defaults");
        logger.LogInformation("");
        logger.LogInformation("OUTPUT:");
        logger.LogInformation("  Analysis/classification results are saved to the 'Output' folder:");
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
