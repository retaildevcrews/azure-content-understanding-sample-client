using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Hosting;
using ContentUnderstanding.Client.Services;
using ContentUnderstanding.Client.Data;
using ContentUnderstanding.Client.Utilities;
using ContentUnderstanding.Client.Models;
using System.CommandLine;
using ContentUnderstanding.Client.Commands;

namespace ContentUnderstanding.Client;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Bootstrap host (configuration, logging, DI)
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.Sources.Clear();
                cfg.SetBasePath(Directory.GetCurrentDirectory());
                cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                cfg.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
                cfg.AddEnvironmentVariables();
                cfg.AddCommandLine(args);
            })
            .ConfigureLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((ctx, services) =>
            {
                ConfigureServices(services, ctx.Configuration);
            })
            .Build();

        using var scope = host.Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("Azure Content Understanding C# Sample Application");
            logger.LogInformation("==================================================");

            // If any args are provided, run the CLI subcommands; otherwise, show interactive overview
            if (args.Length > 0)
            {
                // Strip legacy convenience tokens if present
                var filteredArgs = args
                    .Where(a => !a.Equals("--use-cli", StringComparison.OrdinalIgnoreCase) && !a.Equals("cli", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var root = Cli.BuildRoot(serviceProvider);
                await root.InvokeAsync(filteredArgs);
            }
            else
            {
                await RunInteractiveModeAsync(serviceProvider);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Application failed with exception");
            Environment.Exit(1);
        }
    }

    internal static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
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
    services.AddScoped<ResultExporter>();
    services.AddScoped<BatchSummaryWriter>();
        
        // TODO: Add other services as they are implemented
        // services.AddScoped<ContentUnderstandingService>();
    }

    internal static async Task RunHealthCheckAsync(IServiceProvider serviceProvider)
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
            logger.LogInformation("Run: dotnet run -- --use-cli health for detailed diagnostics.");
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
        logger.LogInformation("Available subcommands (preferred):");
        logger.LogInformation("  health                   Run comprehensive health check");
        logger.LogInformation("  analyzers                List all analyzers");
        logger.LogInformation("  create-analyzer          Create analyzer from JSON");
        logger.LogInformation("  analyze                  Analyze a document");
        logger.LogInformation("  check-operation          Check specific operation status");
        logger.LogInformation("  classifiers              List all classifiers");
        logger.LogInformation("  create-classifier        Create classifier from JSON");
        logger.LogInformation("  classify                 Classify a document with a classifier");
        logger.LogInformation("  classify-dir             Classify all files in a directory");
        logger.LogInformation("");
        
        // TODO: Implement interactive menu for Content Understanding operations
        logger.LogInformation("🚧 Content Understanding operations ready for testing...");
        logger.LogInformation("");
        logger.LogInformation("Next steps:");
        logger.LogInformation("1. Run health check: dotnet run -- --use-cli health");
        logger.LogInformation("2. List analyzers: dotnet run -- --use-cli analyzers");
        logger.LogInformation("3. Create sample analyzer: dotnet run -- --use-cli create-analyzer");
        logger.LogInformation("4. List classifiers: dotnet run -- --use-cli classifiers");
        logger.LogInformation("5. Create classifier: dotnet run -- --use-cli create-classifier --classifier-file <file> --classifier <name>");
        logger.LogInformation("6. Classify: dotnet run -- --use-cli classify --classifier <name> --document <file>");
        logger.LogInformation("7. Classify directory: dotnet run -- --use-cli classify-dir --classifier <name> --directory <subfolder>");
    }

    // Legacy argument parsing removed; use System.CommandLine subcommands

    // Field extraction moved to Utilities.FieldExtractor

    internal static async Task RunListAnalyzersAsync(IServiceProvider serviceProvider)
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

    internal static async Task RunCreateAnalyzerAsync(IServiceProvider serviceProvider, string analyzername, string analyzerFile, bool overwrite = false)
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

            try
            {
                var result = await contentUnderstandingService.CreateOrUpdateAnalyzerAsync(analyzername, jsonContent);
                logger.LogInformation("✅ Successfully created analyzer: {AnalyzerName}", analyzername);
                logger.LogDebug("API Response: {Result}", result);
            }
            catch (HttpRequestException httpEx) when ((httpEx.StatusCode.HasValue && httpEx.StatusCode == System.Net.HttpStatusCode.Conflict) || httpEx.Message.Contains("Conflict") || httpEx.Message.Contains("409"))
            {
                if (!overwrite)
                {
                    logger.LogError("❌ Analyzer already exists (409 Conflict). Re-run with --overwrite to delete and recreate.");
                    return;
                }

                logger.LogWarning("⚠️ Analyzer exists. --overwrite is set, deleting then recreating: {AnalyzerName}", analyzername);
                try
                {
                    await contentUnderstandingService.DeleteAnalyzerAsync(analyzername);
                }
                catch (Exception delEx)
                {
                    logger.LogWarning(delEx, "⚠️ Failed to delete existing analyzer '{AnalyzerName}' before recreate; will still attempt recreate", analyzername);
                }

                var recreate = await contentUnderstandingService.CreateOrUpdateAnalyzerAsync(analyzername, jsonContent);
                logger.LogInformation("✅ Successfully recreated analyzer: {AnalyzerName}", analyzername);
                logger.LogDebug("API Response: {Result}", recreate);
            }
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
    internal static async Task RunAnalysisAsync(IServiceProvider serviceProvider, string analyzerName, string documentFile)
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
            var projectRoot = PathResolver.ProjectRoot();
            var targetDocumentsPath = PathResolver.DataDir();

            string targetDocumentPath;
            string documentFileName;

            if (Path.IsPathFullyQualified(documentFile))
            {
                targetDocumentPath = documentFile;
                documentFileName = Path.GetFileName(documentFile);
            }
            else
            {
                targetDocumentPath = Path.Combine(targetDocumentsPath, documentFile);
                documentFileName = documentFile;
                if (!File.Exists(targetDocumentPath))
                {
                    // Search recursively for the file
                    var found = Directory.EnumerateFiles(targetDocumentsPath, documentFile, SearchOption.AllDirectories).FirstOrDefault();
                    if (found != null)
                    {
                        targetDocumentPath = found;
                        documentFileName = Path.GetFileName(found);
                        logger.LogInformation("🔎 Found document in subdirectory: {Path}", targetDocumentPath);
                    }
                }
            }

            if (!File.Exists(targetDocumentPath))
            {
                logger.LogError("❌ Document not found: {Path}", targetDocumentPath);
                return;
            }

            var documentData = await File.ReadAllBytesAsync(targetDocumentPath);
            logger.LogInformation("🧠 Using analyzer: {Analyzer}", analyzerName);
            logger.LogInformation("📄 Document: {File}", documentFileName);

            var contentType = MimeTypeProvider.GetContentType(targetDocumentPath);

            var analysisResult = await contentUnderstandingService.AnalyzeDocumentAsync(
                analyzerName,
                documentData,
                contentType);

            var exporter = serviceProvider.GetRequiredService<ResultExporter>();
            await HandleAnalysisResultAsync(contentUnderstandingService, analysisResult, logger, documentFileName, exporter);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to analyze");
        }
    }

    // Existing: Default test analysis that picks a known analyzer and sample file when none are provided
    internal static async Task RunTestAnalysisAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();

        logger.LogInformation("🔍 Test document analysis mode (defaults)...");
        logger.LogInformation("");

        try
        {
            var projectRoot = PathResolver.ProjectRoot();
            var targetDocumentDir = PathResolver.DataDir();
            var targetDocumentPath = Path.Combine(targetDocumentDir, "receipt1.pdf");
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

            var exporter = serviceProvider.GetRequiredService<ResultExporter>();
            await HandleAnalysisResultAsync(contentUnderstandingService, analysisResult, logger, documentFileName, exporter);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to run default test analysis");
        }
    }
    
    // Shared result handling for analysis operations
    internal static async Task<(string? operationId, string? jsonPath, string? textPath)> HandleAnalysisResultAsync(
        ContentUnderstandingService contentUnderstandingService,
        (string responseContent, string operationLocation) analysisResult,
        ILogger logger,
        string documentFileName,
        ResultExporter exporter)
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
                var fieldValue = FieldExtractor.ExtractFieldValue(field.Value);
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
                    var cleanedOperationId = CleanOperationId(operationId);
                    logger.LogInformation("🆔 Operation ID: {OperationId}", cleanedOperationId);

                    // Export results to files via ResultExporter
                    var outputDir = PathResolver.OutputDir();
                    var export = await exporter.ExportAsync(resultDoc, outputDir, documentFileName, cleanedOperationId);
                    logger.LogInformation("💾 Results exported to Output folder:");
                    logger.LogInformation("   📄 Raw JSON: {JsonFile}", Path.GetFileName(export.jsonPath));
                    logger.LogInformation("   📝 Formatted (HTML): {TxtFile}", Path.GetFileName(export.formattedPath));
                    logger.LogInformation("   📁 Location: {OutputDir}", outputDir);
                    return (cleanedOperationId, export.jsonPath, export.formattedPath);
                }
                catch (Exception parseEx)
                {
                    logger.LogWarning("⚠️ Could not parse results summary: {Message}", parseEx.Message);
                    logger.LogDebug(parseEx, "Full parsing exception");
                    logger.LogInformation("📋 Operation completed successfully - raw results available via API");

                    // Still try to export raw results
                    var operationId = analysisResult.operationLocation.Split('/').LastOrDefault();
                    var cleanedOperationId = CleanOperationId(operationId);
                    var outputDir = PathResolver.OutputDir();
                    var export = await exporter.ExportAsync(resultDoc, outputDir, documentFileName, cleanedOperationId);
                    logger.LogInformation("💾 Results exported to Output folder:");
                    logger.LogInformation("   📄 Raw JSON: {JsonFile}", Path.GetFileName(export.jsonPath));
                    logger.LogInformation("   📝 Formatted (HTML): {TxtFile}", Path.GetFileName(export.formattedPath));
                    logger.LogInformation("   📁 Location: {OutputDir}", outputDir);
                    return (cleanedOperationId, export.jsonPath, export.formattedPath);
                }
            }
            catch (TimeoutException tex)
            {
                var operationId = analysisResult.operationLocation.Split('/').LastOrDefault();
                var cleanedOperationId = CleanOperationId(operationId);
                logger.LogWarning("⏰ {Message}", tex.Message);
                logger.LogInformation("🆔 Operation ID: {OperationId}", cleanedOperationId);
                logger.LogInformation("💡 You can check the status later using:");
                logger.LogInformation("    dotnet run -- --use-cli check-operation --operation-id {OperationId}", cleanedOperationId);
                logger.LogInformation("💡 Or check the Azure portal for completion status.");
                return (cleanedOperationId, null, null);
            }
            catch (InvalidOperationException ioex)
            {
                logger.LogError("❌ Analysis failed: {Message}", ioex.Message);
                return (null, null, null);
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
            return (null, null, null);
        }
    }
    
    /// <summary>
    /// Checks the status of a specific Content Understanding operation
    /// </summary>
    internal static async Task RunCheckOperationAsync(IServiceProvider serviceProvider, string operationId = "")
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();

        try
        {
            if (string.IsNullOrEmpty(operationId))
            {
                logger.LogError("❌ Operation ID is required for check-operation command");
                logger.LogInformation("💡 Usage: dotnet run -- --use-cli check-operation --operation-id <operation-id>");
                logger.LogInformation("💡 Example: dotnet run -- --use-cli check-operation --operation-id 069e39de-5132-425d-87b7-9f84cd4317f5");
                return;
            }

            var cleanedOperationId = CleanOperationId(operationId);
            logger.LogInformation("🔍 Checking operation: {OperationId}", cleanedOperationId);

            // Try to get the operation result directly
            var resultResponse = await contentUnderstandingService.GetOperationResultAsync(cleanedOperationId!);
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
                        var exporter = serviceProvider.GetRequiredService<ResultExporter>();
                        using (var exportDoc = JsonDocument.Parse(resultResponse))
                        {
                            var outputDir = PathResolver.OutputDir();
                            var export = await exporter.ExportAsync(exportDoc, outputDir, $"operation_{cleanedOperationId}", cleanedOperationId);
                            logger.LogInformation("💾 Results exported to Output folder:");
                            logger.LogInformation("   📄 Raw JSON: {JsonFile}", Path.GetFileName(export.jsonPath));
                            logger.LogInformation("   📝 Formatted (HTML): {TxtFile}", Path.GetFileName(export.formattedPath));
                            logger.LogInformation("   📁 Location: {OutputDir}", outputDir);
                        }
                    }
                    catch
                    {
                        // Fallback to showing key information instead of full JSON
                        logger.LogInformation("📋 Operation completed - check Azure portal for detailed results");
                        
                        // Still try to export raw results
                        var exporter = serviceProvider.GetRequiredService<ResultExporter>();
                        using (var exportDoc = JsonDocument.Parse(resultResponse))
                        {
                            var outputDir = PathResolver.OutputDir();
                            var export = await exporter.ExportAsync(exportDoc, outputDir, $"operation_{cleanedOperationId}", cleanedOperationId);
                            logger.LogInformation("💾 Results exported to Output folder:");
                            logger.LogInformation("   📄 Raw JSON: {JsonFile}", Path.GetFileName(export.jsonPath));
                            logger.LogInformation("   📝 Formatted (HTML): {TxtFile}", Path.GetFileName(export.formattedPath));
                            logger.LogInformation("   📁 Location: {OutputDir}", outputDir);
                        }
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
    
    internal static async Task RunListClassifiersAsync(IServiceProvider serviceProvider)
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
    internal static async Task RunCreateClassifierAsync(IServiceProvider serviceProvider, string classifierName, string classifierFile, bool overwrite = false)
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

            try
            {
                var result = await contentUnderstandingService.CreateOrUpdateClassifierAsync(classifierName, jsonContent);
                logger.LogInformation("✅ Successfully created classifier: {ClassifierName}", classifierName);
                logger.LogDebug("API Response: {Result}", result);
            }
            catch (HttpRequestException httpEx) when ((httpEx.StatusCode.HasValue && httpEx.StatusCode == System.Net.HttpStatusCode.Conflict) || httpEx.Message.Contains("Conflict") || httpEx.Message.Contains("409"))
            {
                if (!overwrite)
                {
                    logger.LogError("❌ Classifier already exists (409 Conflict). Re-run with --overwrite to delete and recreate.");
                    return;
                }

                logger.LogWarning("⚠️ Classifier exists. --overwrite is set, deleting then recreating: {ClassifierName}", classifierName);
                try
                {
                    await contentUnderstandingService.DeleteClassifierAsync(classifierName);
                }
                catch (Exception delEx)
                {
                    logger.LogWarning(delEx, "⚠️ Failed to delete existing classifier '{ClassifierName}' before recreate; will still attempt recreate", classifierName);
                }

                var recreate = await contentUnderstandingService.CreateOrUpdateClassifierAsync(classifierName, jsonContent);
                logger.LogInformation("✅ Successfully recreated classifier: {ClassifierName}", classifierName);
                logger.LogDebug("API Response: {Result}", recreate);
            }
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
    internal static async Task RunClassifyAsync(IServiceProvider serviceProvider, string classifierName, string documentFile, string text = "")
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

            // Require documentFile
            if (string.IsNullOrWhiteSpace(documentFile))
            {
                logger.LogError("❌ Provide --document <file> to classify a document");
                return;
            }

            var projectRoot = PathResolver.ProjectRoot();
            var targetDocumentDir = PathResolver.DataDir();

            string targetDocumentPath;
            string documentFileName;

            if (Path.IsPathFullyQualified(documentFile))
            {
                targetDocumentPath = documentFile;
                documentFileName = Path.GetFileName(documentFile);
            }
            else
            {
                targetDocumentPath = Path.Combine(targetDocumentDir, documentFile);
                documentFileName = documentFile;
                if (!File.Exists(targetDocumentPath))
                {
                    // Search recursively for the file
                    var found = Directory.EnumerateFiles(targetDocumentDir, documentFile, SearchOption.AllDirectories).FirstOrDefault();
                    if (found != null)
                    {
                        targetDocumentPath = found;
                        documentFileName = Path.GetFileName(found);
                        logger.LogInformation("Found document in subdirectory: {Path}", targetDocumentPath);
                    }
                }
            }

            if (!File.Exists(targetDocumentPath))
            {
                logger.LogError("❌ Document not found: {Path}", targetDocumentPath);
                return;
            }

            var bytes = await File.ReadAllBytesAsync(targetDocumentPath);
            var contentType = MimeTypeProvider.GetContentType(targetDocumentPath);

            logger.LogInformation("🧠 Using classifier: {Classifier}", classifierName);
            logger.LogInformation("📄 Document: {File}", documentFileName);

            var classifyResultFile = await contentUnderstandingService.ClassifyAsync(classifierName, bytes, contentType);
            var exporter = serviceProvider.GetRequiredService<ResultExporter>();
            await HandleAnalysisResultAsync(contentUnderstandingService, classifyResultFile, logger, documentFileName, exporter);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to classify");
        }
    }

    // NEW: Classify all supported files in a directory under Data/SampleDocuments (non-recursive)
    internal static async Task RunClassifyDirectoryAsync(IServiceProvider serviceProvider, string classifierName, string directoryName)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var contentUnderstandingService = serviceProvider.GetRequiredService<ContentUnderstandingService>();

        logger.LogInformation("📂 Classify directory mode...");
        logger.LogInformation("");

        try
        {
            if (string.IsNullOrWhiteSpace(classifierName))
            {
                logger.LogError("❌ Please specify a classifier with --classifier <name>");
                return;
            }

            if (string.IsNullOrWhiteSpace(directoryName))
            {
                logger.LogError("❌ Please specify a directory with --directory <subfolder>");
                return;
            }

            var projectRoot = PathResolver.ProjectRoot();
            var baseDir = PathResolver.DataDir();
            var targetDir = Path.Combine(baseDir, directoryName);

            if (!Directory.Exists(targetDir))
            {
                logger.LogError("❌ Directory not found: {Dir}", targetDir);
                return;
            }

            // Supported file extensions
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".pdf", ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp"
            };

            var files = Directory.EnumerateFiles(targetDir, "*", SearchOption.TopDirectoryOnly)
                .Where(f => allowed.Contains(Path.GetExtension(f)))
                .OrderBy(f => f)
                .ToList();

            if (files.Count == 0)
            {
                logger.LogWarning("⚠️ No supported files found in {Dir}", targetDir);
                return;
            }

            logger.LogInformation("🧠 Using classifier: {Classifier}", classifierName);
            logger.LogInformation("📁 Directory: {Dir} ({Count} files)", directoryName, files.Count);

            // Summary tracking
            var summary = new List<BatchSummaryRow>();
            int success = 0, failed = 0;

            var exporter = serviceProvider.GetRequiredService<ResultExporter>();
            foreach (var filePath in files)
            {
                var fileName = Path.GetFileName(filePath);
                var contentType = MimeTypeProvider.GetContentType(filePath);

                var sw = Stopwatch.StartNew();
                string status = "Succeeded";
                string? operationId = null;
                string? jsonFile = null;
                string? txtFile = null;
                string? error = null;

                try
                {
                    var bytes = await File.ReadAllBytesAsync(filePath);
                    var result = await contentUnderstandingService.ClassifyAsync(classifierName, bytes, contentType);

                    // Poll and export via shared handler, but also capture the produced filenames by peeking Output directory after call
                    // For deterministic capture, rely on handler to export and just proceed; we'll scan Output to find the newest pair for this document.
                    var res = await HandleAnalysisResultAsync(contentUnderstandingService, result, logger, fileName, exporter);
                    operationId = res.operationId;
                    jsonFile = res.jsonPath;
                    txtFile = res.textPath;

                    success++;
                }
                catch (Exception ex)
                {
                    status = "Failed";
                    error = ex.Message;
                    failed++;
                    logger.LogError(ex, "❌ Failed to classify {File}", fileName);
                }
                finally
                {
                    sw.Stop();
                    summary.Add(new BatchSummaryRow
                    {
                        File = fileName,
                        Status = status,
                        OperationId = operationId,
                        JsonPath = jsonFile,
                        TextPath = txtFile,
                        DurationMs = sw.ElapsedMilliseconds,
                        Error = error
                    });
                }
            }

            var summaryWriter = serviceProvider.GetRequiredService<BatchSummaryWriter>();
            var summaryPath = await summaryWriter.ExportAsync(directoryName, classifierName, summary);
            if (!string.IsNullOrEmpty(summaryPath))
            {
                logger.LogInformation("📦 Summary exported: {Path}", Path.GetFileName(summaryPath));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to classify directory");
        }
    }

    // Batch summary moved to Services.BatchSummaryWriter and Models.BatchSummaryRow

    /// <summary>
    /// Displays help information about available commands and usage
    /// </summary>
    private static void DisplayHelpInformation(ILogger logger)
    {
        logger.LogInformation("Azure Content Understanding C# Sample Application");
        logger.LogInformation("==================================================");
        logger.LogInformation("");
    logger.LogInformation("USAGE:");
    logger.LogInformation("  dotnet run -- --use-cli <command> [options]");
    logger.LogInformation("");
    logger.LogInformation("COMMANDS (preferred):");
    logger.LogInformation("  health                    Run comprehensive health check");
    logger.LogInformation("  analyzers                 List all analyzers");
    logger.LogInformation("  analyze                   Analyze a document");
    logger.LogInformation("  check-operation           Check operation status");
    logger.LogInformation("  classifiers               List classifiers");
    logger.LogInformation("  create-classifier         Create classifier from JSON");
    logger.LogInformation("  create-analyzer           Create analyzer from JSON");
    logger.LogInformation("  classify                  Classify a document");
    logger.LogInformation("  classify-dir              Classify all files in a directory");
    logger.LogInformation("");
        logger.LogInformation("OPTIONS:");
        logger.LogInformation("  --analyzer-file <file>  Specify analyzer JSON file for create-analyzer mode");
        logger.LogInformation("                         (looks in Data folder, supports partial names)");
        logger.LogInformation("  --analyzer <name>      Specify analyzer name for analysis mode");
    logger.LogInformation("  --document <file>      Specify document file for analysis or classification");
    logger.LogInformation("                         (looks in Data/SampleDocuments or absolute path)");
        logger.LogInformation("  --classifier <name>    Specify classifier name for classification");
        logger.LogInformation("  --classifier-file <f>  Specify classifier JSON file for create-classifier");
        logger.LogInformation("  --directory <subdir>   Directory under Data/SampleDocuments for classify-dir");
    // --text removed: only document classification supported
        logger.LogInformation("");
    logger.LogInformation("EXAMPLES:");
    logger.LogInformation("  dotnet run                                          # Interactive mode");
    logger.LogInformation("  dotnet run -- --use-cli health                      # Health check only");
    logger.LogInformation("  dotnet run -- --use-cli analyzers                   # List analyzers");
    logger.LogInformation("  dotnet run -- --use-cli create-analyzer --analyzer-file receipt.json --analyzer receipt");
    logger.LogInformation("  dotnet run -- --use-cli analyze --analyzer receipt --document receipt1.pdf");
    logger.LogInformation("  dotnet run -- --use-cli check-operation --operation-id 069e39de-5132-425d-87b7-9f84cd4317f5");
    logger.LogInformation("  dotnet run -- --use-cli classifiers                 # List classifiers");
    logger.LogInformation("  dotnet run -- --use-cli create-classifier --classifier-file product-categories.json --classifier products");
    logger.LogInformation("  dotnet run -- --use-cli classify --classifier products --document sample.png");
    logger.LogInformation("  dotnet run -- --use-cli classify-dir --classifier products --directory receipts");
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
    logger.LogInformation("  • Formatted results (HTML): *_formatted.html");
        logger.LogInformation("");
        logger.LogInformation("For more information, run in interactive mode or check the documentation.");
    }

    // Export and formatting moved to Services.ResultExporter

    private static string? CleanOperationId(string? operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId)) return operationId;
        var clean = operationId.Split('?')[0];
        if (clean.Contains('_')) clean = clean.Split('_')[0];
        return clean;
    }
}
