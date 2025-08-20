using System.Text.Json.Serialization;

namespace ContentUnderstanding.Models;

/// <summary>
/// Represents an analyzer definition for Content Understanding
/// </summary>
public class AnalyzerDefinition
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("baseAnalyzerId")]
    public string? BaseAnalyzerId { get; set; }

    [JsonPropertyName("fields")]
    public List<FieldDefinition> Fields { get; set; } = new();

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "documentAnalyzer";

    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "2025-05-01-preview";
}

/// <summary>
/// Represents a field definition within an analyzer
/// </summary>
public class FieldDefinition
{
    [JsonPropertyName("fieldKey")]
    public string FieldKey { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("fieldType")]
    public string FieldType { get; set; } = "string";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("example")]
    public string? Example { get; set; }

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; } = false;
}
