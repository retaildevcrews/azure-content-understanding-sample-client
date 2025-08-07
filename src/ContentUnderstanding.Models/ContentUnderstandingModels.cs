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

/// <summary>
/// Response from listing analyzers
/// </summary>
public class AnalyzersResponse
{
    [JsonPropertyName("value")]
    public List<AnalyzerInfo> Value { get; set; } = new();

    [JsonPropertyName("nextLink")]
    public string? NextLink { get; set; }
}

/// <summary>
/// Basic information about an analyzer
/// </summary>
public class AnalyzerInfo
{
    [JsonPropertyName("analyzerName")]
    public string AnalyzerName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("createdDateTime")]
    public DateTime? CreatedDateTime { get; set; }

    [JsonPropertyName("lastModifiedDateTime")]
    public DateTime? LastModifiedDateTime { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = string.Empty;
}

/// <summary>
/// Analysis result response
/// </summary>
public class AnalysisResult
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("createdDateTime")]
    public DateTime CreatedDateTime { get; set; }

    [JsonPropertyName("lastUpdatedDateTime")]
    public DateTime LastUpdatedDateTime { get; set; }

    [JsonPropertyName("result")]
    public DocumentResult? Result { get; set; }

    [JsonPropertyName("error")]
    public ApiError? Error { get; set; }
}

/// <summary>
/// Document analysis result
/// </summary>
public class DocumentResult
{
    [JsonPropertyName("fields")]
    public Dictionary<string, ExtractedField> Fields { get; set; } = new();

    [JsonPropertyName("documents")]
    public List<AnalyzedDocument> Documents { get; set; } = new();
}

/// <summary>
/// Extracted field value
/// </summary>
public class ExtractedField
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public object? Value { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("boundingRegions")]
    public List<BoundingRegion>? BoundingRegions { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("spans")]
    public List<DocumentSpan>? Spans { get; set; }
}

/// <summary>
/// Analyzed document information
/// </summary>
public class AnalyzedDocument
{
    [JsonPropertyName("docType")]
    public string DocType { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public Dictionary<string, ExtractedField> Fields { get; set; } = new();

    [JsonPropertyName("boundingRegions")]
    public List<BoundingRegion>? BoundingRegions { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("spans")]
    public List<DocumentSpan>? Spans { get; set; }
}

/// <summary>
/// Bounding region for extracted content
/// </summary>
public class BoundingRegion
{
    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; }

    [JsonPropertyName("polygon")]
    public List<double> Polygon { get; set; } = new();
}

/// <summary>
/// Document span information
/// </summary>
public class DocumentSpan
{
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("length")]
    public int Length { get; set; }
}

/// <summary>
/// API error response
/// </summary>
public class ApiError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public List<ApiError>? Details { get; set; }
}

/// <summary>
/// Operation result for async operations
/// </summary>
public class OperationResult
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("createdDateTime")]
    public DateTime CreatedDateTime { get; set; }

    [JsonPropertyName("lastUpdatedDateTime")]
    public DateTime LastUpdatedDateTime { get; set; }

    [JsonPropertyName("percentCompleted")]
    public int? PercentCompleted { get; set; }

    [JsonPropertyName("result")]
    public DocumentResult? Result { get; set; }

    [JsonPropertyName("error")]
    public ApiError? Error { get; set; }
}
