using System.Text.Json;

namespace ContentUnderstanding.Client.Data;

/// <summary>
/// Utility class for loading sample analyzer schemas from JSON files
/// </summary>
public static class SampleAnalyzers
{
    /// <summary>
    /// Loads an analyzer JSON by either:
    ///  1. Using an absolute path directly, OR
    ///  2. Treating the argument as a relative path under Data/, OR
    ///  3. Recursively searching Data/ for a filename match (case-insensitive) when not found directly.
    /// No partial / substring matching is performed (exact filename only when searching).
    /// </summary>
    public static async Task<string> LoadAnalyzerJsonAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Analyzer file name is required", nameof(fileName));

        // Absolute path first
        if (Path.IsPathFullyQualified(fileName) && File.Exists(fileName))
        {
            return await File.ReadAllTextAsync(fileName);
        }

    var dataDir = ContentUnderstanding.Client.Utilities.PathResolver.DataDir();

        // If caller provided a relative subpath (contains directory separator), honor it directly
        if (fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
        {
            var combined = Path.Combine(dataDir, fileName);
            if (File.Exists(combined))
            {
                return await File.ReadAllTextAsync(combined);
            }
            throw new FileNotFoundException($"Analyzer JSON file '{fileName}' not found under '{dataDir}'.");
        }

        // Search recursively for an exact filename match
        var matches = Directory.EnumerateFiles(dataDir, fileName, SearchOption.AllDirectories)
                               .Where(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase))
                               .Take(2) // we only need to know if >1
                               .ToList();

        if (matches.Count == 1)
        {
            return await File.ReadAllTextAsync(matches[0]);
        }
        if (matches.Count > 1)
        {
            throw new InvalidOperationException($"Multiple analyzer files named '{fileName}' found under '{dataDir}'. Please specify a relative path.");
        }

        throw new FileNotFoundException($"Analyzer JSON file '{fileName}' not found under '{dataDir}'.");
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
            //return root.TryGetProperty("description", out _) &&
            //       root.TryGetProperty("baseAnalyzerId", out _) &&
            //       root.TryGetProperty("fieldSchema", out _);
            return root.TryGetProperty("baseAnalyzerId", out _) &&
                   root.TryGetProperty("fieldSchema", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
