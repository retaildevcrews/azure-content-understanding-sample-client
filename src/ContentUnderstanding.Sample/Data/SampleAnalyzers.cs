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
