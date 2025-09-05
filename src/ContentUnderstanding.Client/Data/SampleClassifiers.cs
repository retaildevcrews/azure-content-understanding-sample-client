using System.Text.Json;

namespace ContentUnderstanding.Client.Data;

public static class SampleClassifiers
{
    /// <summary>
    /// Loads a classifier JSON definition via:
    ///  1. Absolute path (if provided and exists)
    ///  2. Relative path under Data/SampleClassifiers (if includes directory separators)
    ///  3. Recursive exact filename search under Data/SampleClassifiers (case-insensitive)
    /// No partial name matching is performed to avoid ambiguity.
    /// </summary>
    public static async Task<string> LoadClassifierJsonAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Classifier file name is required", nameof(fileName));

    var dataDir = Path.Combine(ContentUnderstanding.Client.Utilities.PathResolver.DataDir(), "SampleClassifiers");

        // Absolute path
        if (Path.IsPathFullyQualified(fileName) && File.Exists(fileName))
        {
            return await File.ReadAllTextAsync(fileName);
        }

        // Relative subpath provided
        if (fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
        {
            var combined = Path.Combine(dataDir, fileName);
            if (File.Exists(combined))
            {
                return await File.ReadAllTextAsync(combined);
            }
            throw new FileNotFoundException($"Classifier JSON file '{fileName}' not found under '{dataDir}'.");
        }

        // Recursive exact filename search
        var matches = Directory.EnumerateFiles(dataDir, fileName, SearchOption.AllDirectories)
                               .Where(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase))
                               .Take(2)
                               .ToList();
        if (matches.Count == 1)
        {
            return await File.ReadAllTextAsync(matches[0]);
        }
        if (matches.Count > 1)
        {
            throw new InvalidOperationException($"Multiple classifier files named '{fileName}' found under '{dataDir}'. Please specify a relative path.");
        }

        throw new FileNotFoundException($"Classifier JSON file '{fileName}' not found under '{dataDir}'.");
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