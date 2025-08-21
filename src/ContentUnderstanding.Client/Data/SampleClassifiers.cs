using System.Text.Json;

namespace ContentUnderstanding.Client.Data;

public static class SampleClassifiers
{
    /// Loads a classifier JSON definition from the Data folder (supports partial matches).
    public static async Task<string> LoadClassifierJsonAsync(string fileName)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dataDir = Path.Combine(baseDir, "Data\\SampleClassifiers");

        // If a direct path is provided and exists, use it
        if (Path.IsPathFullyQualified(fileName) && File.Exists(fileName))
        {
            return await File.ReadAllTextAsync(fileName);
        }

        // Try exact match in Data
        var direct = Path.Combine(dataDir, fileName);
        if (File.Exists(direct))
        {
            return await File.ReadAllTextAsync(direct);
        }

        // Try case-insensitive partial match in Data
        var candidates = Directory.EnumerateFiles(dataDir, "*.json", SearchOption.TopDirectoryOnly)
                                  .Where(f => Path.GetFileName(f).Contains(fileName, StringComparison.OrdinalIgnoreCase))
                                  .ToList();
        if (candidates.Count == 1)
        {
            return await File.ReadAllTextAsync(candidates[0]);
        }
        if (candidates.Count > 1)
        {
            throw new InvalidOperationException($"Multiple files match '{fileName}'. Please be more specific.");
        }

        throw new FileNotFoundException($"Classifier JSON file '{fileName}' not found in '{dataDir}'");
    }

    /// Minimal validation that content is JSON and not empty.
    /// You can extend this with schema-specific checks (e.g., required properties).
    public static bool ValidateClassifierJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}