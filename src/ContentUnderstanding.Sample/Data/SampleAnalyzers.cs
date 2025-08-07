using System.Text.Json;

namespace ContentUnderstanding.Sample.Data;

/// <summary>
/// Utility class for loading sample analyzer schemas from JSON files
/// </summary>
public static class SampleAnalyzers
{
    /// <summary>
    /// Loads an analyzer definition from a JSON file
    /// </summary>
    /// <param name="fileName">Name of the JSON file (without path)</param>
    /// <returns>The raw JSON content as a string</returns>
    public static async Task<string> LoadAnalyzerJsonAsync(string fileName)
    {
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", fileName);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Analyzer JSON file not found: {fileName}");
        }

        return await File.ReadAllTextAsync(filePath);
    }

    /// <summary>
    /// Gets all available analyzer JSON files in the Data directory
    /// </summary>
    /// <returns>Dictionary with analyzer name as key and file path as value</returns>
    public static Dictionary<string, string> GetAvailableAnalyzers()
    {
        var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        var analyzers = new Dictionary<string, string>();

        if (Directory.Exists(dataPath))
        {
            var jsonFiles = Directory.GetFiles(dataPath, "*-Analyzer_*.json");
            
            foreach (var file in jsonFiles)
            {
                var fileName = Path.GetFileName(file);
                // Extract analyzer name from filename (e.g., "receipt-Analyzer_2025-05-01-preview.json" -> "receipt")
                var analyzerName = fileName.Split('-')[0];
                analyzers[analyzerName] = fileName;
            }
        }

        return analyzers;
    }

    /// <summary>
    /// Validates that a JSON string contains a valid analyzer definition
    /// </summary>
    /// <param name="json">JSON string to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateAnalyzerJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // Check for required properties
            return root.TryGetProperty("description", out _) &&
                   root.TryGetProperty("baseAnalyzerId", out _) &&
                   root.TryGetProperty("fieldSchema", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
