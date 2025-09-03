using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ContentUnderstanding.Client.Commands;

internal static class Cli
{
    public static RootCommand BuildRoot(IServiceProvider services)
    {
        var root = new RootCommand("Azure Content Understanding sample");

        // health
        var health = new Command("health", "Run health check");
        health.SetHandler(async () =>
        {
            await Program.RunHealthCheckAsync(services);
        });
        root.Add(health);

        // analyzers list
        var analyzers = new Command("analyzers", "List analyzers");
        analyzers.SetHandler(async () =>
        {
            await Program.RunListAnalyzersAsync(services);
        });
        root.Add(analyzers);

    // analyze
    var analyzerOption = new Option<string>(name: "--analyzer", description: "Analyzer name", getDefaultValue: () => string.Empty);
    var documentOption = new Option<string>(name: "--document", description: "Document file (relative to Data/SampleDocuments or absolute)", getDefaultValue: () => string.Empty);
    var analyze = new Command("analyze", "Analyze a document");
    analyze.AddOption(analyzerOption);
    analyze.AddOption(documentOption);
    analyze.SetHandler(async (string analyzer, string document) =>
        {
            if (string.IsNullOrWhiteSpace(analyzer) || string.IsNullOrWhiteSpace(document))
            {
                await Program.RunTestAnalysisAsync(services);
            }
            else
            {
                await Program.RunAnalysisAsync(services, analyzer, document);
            }
    }, analyzerOption, documentOption);
        root.Add(analyze);

        // check-operation
    var operationIdOption = new Option<string>(name: "--operation-id", description: "Operation ID", getDefaultValue: () => string.Empty);
    var op = new Command("check-operation", "Check operation status");
    op.AddOption(operationIdOption);
    op.SetHandler(async (string operationId) =>
        {
            await Program.RunCheckOperationAsync(services, operationId);
    }, operationIdOption);
        root.Add(op);

        // classifiers list
        var classifiers = new Command("classifiers", "List classifiers");
        classifiers.SetHandler(async () =>
        {
            await Program.RunListClassifiersAsync(services);
        });
        root.Add(classifiers);

        // create-classifier
    var classifierNameOption = new Option<string>(name: "--classifier", description: "Classifier name", getDefaultValue: () => string.Empty);
    var classifierFileOption = new Option<string>(name: "--classifier-file", description: "Classifier JSON file", getDefaultValue: () => string.Empty);
    var createClf = new Command("create-classifier", "Create classifier from JSON");
    var overwriteOption = new Option<bool>(name: "--overwrite", description: "If the classifier exists (409), delete and recreate", getDefaultValue: () => false);
    createClf.AddOption(classifierNameOption);
    createClf.AddOption(classifierFileOption);
    createClf.AddOption(overwriteOption);
    createClf.SetHandler(async (string classifier, string file, bool overwrite) =>
        {
            await Program.RunCreateClassifierAsync(services, classifier, file, overwrite);
    }, classifierNameOption, classifierFileOption, overwriteOption);
        root.Add(createClf);

        // classify
    var classifyDocOption = new Option<string>(name: "--document", description: "Document file (relative to Data/SampleDocuments or absolute)", getDefaultValue: () => string.Empty);
    var classify = new Command("classify", "Classify a document");
    classify.AddOption(classifierNameOption);
    classify.AddOption(classifyDocOption);
    classify.SetHandler(async (string classifierName, string document) =>
        {
            await Program.RunClassifyAsync(services, classifierName, document);
    }, classifierNameOption, classifyDocOption);
        root.Add(classify);

        // classify-dir
        var directoryOption = new Option<string>(name: "--directory", description: "Subdirectory under Data/SampleDocuments", getDefaultValue: () => string.Empty);
        var classifyDir = new Command("classify-dir", "Classify all supported files in a directory");
        classifyDir.AddOption(classifierNameOption);
        classifyDir.AddOption(directoryOption);
        classifyDir.SetHandler(async (string classifierName, string directory) =>
        {
            await Program.RunClassifyDirectoryAsync(services, classifierName, directory);
        }, classifierNameOption, directoryOption);
        root.Add(classifyDir);

        // create-analyzer
        var analyzerNameOption = new Option<string>(name: "--analyzer", description: "Analyzer name", getDefaultValue: () => string.Empty);
        var analyzerFileOption = new Option<string>(name: "--analyzer-file", description: "Analyzer JSON file", getDefaultValue: () => string.Empty);
        var createAnalyzer = new Command("create-analyzer", "Create analyzer from JSON");
        createAnalyzer.AddOption(analyzerNameOption);
        createAnalyzer.AddOption(analyzerFileOption);
        createAnalyzer.SetHandler(async (string analyzer, string file) =>
        {
            await Program.RunCreateAnalyzerAsync(services, analyzer, file);
        }, analyzerNameOption, analyzerFileOption);
        root.Add(createAnalyzer);

        return root;
    }
}
