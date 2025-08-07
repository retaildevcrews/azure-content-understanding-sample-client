using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ContentUnderstanding.Sample.Services;

/// <summary>
/// Service for making HTTP calls to Azure Content Understanding API
/// </summary>
public class ContentUnderstandingService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ContentUnderstandingService> _logger;
    private readonly HttpClient _httpClient;
    private readonly DefaultAzureCredential _credential;
    private string? _cachedApiKey;

    public ContentUnderstandingService(
        IConfiguration configuration, 
        ILogger<ContentUnderstandingService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = new HttpClient();
        
        // Configure HTTP client with reasonable timeouts
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Content Understanding can take time
        
        // Initialize Azure credential for Key Vault access
        var options = new DefaultAzureCredentialOptions
        {
            ExcludeAzureCliCredential = false,
            ExcludeEnvironmentCredential = false,
            ExcludeManagedIdentityCredential = true, // Disable for local dev
            ExcludeVisualStudioCredential = false
        };
        _credential = new DefaultAzureCredential(options);
    }

    /// <summary>
    /// Gets the API key from Key Vault with caching
    /// </summary>
    private async Task<string> GetApiKeyAsync()
    {
        // Thread-safe caching of API key
        if (_cachedApiKey != null)
            return _cachedApiKey;

        // Use a semaphore for async-safe locking
        var keyVaultUri = _configuration["AzureKeyVault:VaultUri"];
        if (string.IsNullOrEmpty(keyVaultUri))
        {
            throw new InvalidOperationException("Key Vault URI not configured. Set AzureKeyVault:VaultUri in configuration.");
        }

        var secretClient = new SecretClient(new Uri(keyVaultUri), _credential);
        
        try
        {
            var secretResponse = await secretClient.GetSecretAsync("ai-services-key");
            _cachedApiKey = secretResponse.Value.Value;
            _logger.LogDebug("Successfully retrieved API key from Key Vault");
            return _cachedApiKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve API key from Key Vault");
            throw new InvalidOperationException("Unable to retrieve Content Understanding API key from Key Vault", ex);
        }
    }

    /// <summary>
    /// Configures HTTP client with authentication headers
    /// </summary>
    private async Task ConfigureAuthenticationAsync()
    {
        var apiKey = await GetApiKeyAsync();
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
    }

    /// <summary>
    /// Gets the Content Understanding endpoint from configuration
    /// </summary>
    private string GetEndpoint()
    {
        var endpoint = _configuration["AzureContentUnderstanding:Endpoint"];
        if (string.IsNullOrEmpty(endpoint))
        {
            throw new InvalidOperationException("Content Understanding endpoint not configured. Set AzureContentUnderstanding:Endpoint in configuration.");
        }
        return endpoint.TrimEnd('/');
    }

    /// <summary>
    /// Lists all analyzers in the Content Understanding service
    /// </summary>
    public async Task<string> ListAnalyzersAsync()
    {
        _logger.LogInformation("📋 Listing all analyzers...");
        
        try
        {
            await ConfigureAuthenticationAsync();
            var endpoint = GetEndpoint();
            
            var url = $"{endpoint}/contentunderstanding/analyzers?api-version=2025-05-01-preview";
            _logger.LogDebug("GET {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully retrieved analyzers list");
                return content;
            }
            else
            {
                _logger.LogError("❌ Failed to list analyzers. Status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, content);
                throw new HttpRequestException($"Failed to list analyzers: {response.StatusCode} - {content}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing analyzers");
            throw;
        }
    }

    /// <summary>
    /// Gets a specific analyzer by name
    /// </summary>
    public async Task<string> GetAnalyzerAsync(string analyzerName)
    {
        if (string.IsNullOrEmpty(analyzerName))
            throw new ArgumentException("Analyzer name cannot be null or empty", nameof(analyzerName));

        _logger.LogInformation("🔍 Getting analyzer: {AnalyzerName}", analyzerName);
        
        try
        {
            await ConfigureAuthenticationAsync();
            var endpoint = GetEndpoint();
            
            var url = $"{endpoint}/contentunderstanding/analyzers/{Uri.EscapeDataString(analyzerName)}?api-version=2025-05-01-preview";
            _logger.LogDebug("GET {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully retrieved analyzer: {AnalyzerName}", analyzerName);
                return content;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("⚠️ Analyzer not found: {AnalyzerName}", analyzerName);
                throw new InvalidOperationException($"Analyzer '{analyzerName}' not found");
            }
            else
            {
                _logger.LogError("❌ Failed to get analyzer {AnalyzerName}. Status: {StatusCode}, Response: {Response}", 
                    analyzerName, response.StatusCode, content);
                throw new HttpRequestException($"Failed to get analyzer '{analyzerName}': {response.StatusCode} - {content}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analyzer: {AnalyzerName}", analyzerName);
            throw;
        }
    }

    /// <summary>
    /// Creates or updates an analyzer schema
    /// </summary>
    public async Task<string> CreateOrUpdateAnalyzerAsync(string analyzerName, string schemaJson)
    {
        if (string.IsNullOrEmpty(analyzerName))
            throw new ArgumentException("Analyzer name cannot be null or empty", nameof(analyzerName));
        if (string.IsNullOrEmpty(schemaJson))
            throw new ArgumentException("Schema JSON cannot be null or empty", nameof(schemaJson));

        _logger.LogInformation("📝 Creating/updating analyzer: {AnalyzerName}", analyzerName);
        
        try
        {
            await ConfigureAuthenticationAsync();
            var endpoint = GetEndpoint();
            
            var url = $"{endpoint}/contentunderstanding/analyzers/{Uri.EscapeDataString(analyzerName)}?api-version=2025-05-01-preview";
            _logger.LogDebug("PUT {Url}", url);
            
            var requestContent = new StringContent(schemaJson, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, requestContent);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully created/updated analyzer: {AnalyzerName}", analyzerName);
                return responseContent;
            }
            else
            {
                _logger.LogError("❌ Failed to create/update analyzer {AnalyzerName}. Status: {StatusCode}, Response: {Response}", 
                    analyzerName, response.StatusCode, responseContent);
                throw new HttpRequestException($"Failed to create/update analyzer '{analyzerName}': {response.StatusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating analyzer: {AnalyzerName}", analyzerName);
            throw;
        }
    }

    /// <summary>
    /// Deletes an analyzer
    /// </summary>
    public async Task DeleteAnalyzerAsync(string analyzerName)
    {
        if (string.IsNullOrEmpty(analyzerName))
            throw new ArgumentException("Analyzer name cannot be null or empty", nameof(analyzerName));

        _logger.LogInformation("🗑️ Deleting analyzer: {AnalyzerName}", analyzerName);
        
        try
        {
            await ConfigureAuthenticationAsync();
            var endpoint = GetEndpoint();
            
            var url = $"{endpoint}/contentunderstanding/analyzers/{Uri.EscapeDataString(analyzerName)}?api-version=2025-05-01-preview";
            _logger.LogDebug("DELETE {Url}", url);
            
            var response = await _httpClient.DeleteAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully deleted analyzer: {AnalyzerName}", analyzerName);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("⚠️ Analyzer not found (already deleted?): {AnalyzerName}", analyzerName);
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("❌ Failed to delete analyzer {AnalyzerName}. Status: {StatusCode}, Response: {Response}", 
                    analyzerName, response.StatusCode, responseContent);
                throw new HttpRequestException($"Failed to delete analyzer '{analyzerName}': {response.StatusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting analyzer: {AnalyzerName}", analyzerName);
            throw;
        }
    }

    /// <summary>
    /// Submits a document for analysis
    /// </summary>
    public async Task<string> AnalyzeDocumentAsync(string analyzerName, byte[] documentData, string contentType)
    {
        if (string.IsNullOrEmpty(analyzerName))
            throw new ArgumentException("Analyzer name cannot be null or empty", nameof(analyzerName));
        if (documentData == null || documentData.Length == 0)
            throw new ArgumentException("Document data cannot be null or empty", nameof(documentData));
        if (string.IsNullOrEmpty(contentType))
            throw new ArgumentException("Content type cannot be null or empty", nameof(contentType));

        _logger.LogInformation("🔍 Analyzing document with analyzer: {AnalyzerName} (Size: {Size} bytes)", 
            analyzerName, documentData.Length);
        
        try
        {
            await ConfigureAuthenticationAsync();
            var endpoint = GetEndpoint();
            
            var url = $"{endpoint}/contentunderstanding/analyzers/{Uri.EscapeDataString(analyzerName)}:analyze?api-version=2025-05-01-preview";
            _logger.LogDebug("POST {Url}", url);
            
            var requestContent = new ByteArrayContent(documentData);
            requestContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            
            var response = await _httpClient.PostAsync(url, requestContent);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully submitted document for analysis with analyzer: {AnalyzerName}", analyzerName);
                return responseContent;
            }
            else
            {
                _logger.LogError("❌ Failed to analyze document with analyzer {AnalyzerName}. Status: {StatusCode}, Response: {Response}", 
                    analyzerName, response.StatusCode, responseContent);
                throw new HttpRequestException($"Failed to analyze document with analyzer '{analyzerName}': {response.StatusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing document with analyzer: {AnalyzerName}", analyzerName);
            throw;
        }
    }

    /// <summary>
    /// Gets the result of a previously submitted analysis operation
    /// </summary>
    public async Task<string> GetAnalysisResultAsync(string operationId)
    {
        if (string.IsNullOrEmpty(operationId))
            throw new ArgumentException("Operation ID cannot be null or empty", nameof(operationId));

        _logger.LogInformation("📊 Getting analysis result for operation: {OperationId}", operationId);
        
        try
        {
            await ConfigureAuthenticationAsync();
            var endpoint = GetEndpoint();
            
            var url = $"{endpoint}/contentunderstanding/operations/{Uri.EscapeDataString(operationId)}?api-version=2025-05-01-preview";
            _logger.LogDebug("GET {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully retrieved analysis result for operation: {OperationId}", operationId);
                return responseContent;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("⚠️ Operation not found: {OperationId}", operationId);
                throw new InvalidOperationException($"Operation '{operationId}' not found");
            }
            else
            {
                _logger.LogError("❌ Failed to get analysis result for operation {OperationId}. Status: {StatusCode}, Response: {Response}", 
                    operationId, response.StatusCode, responseContent);
                throw new HttpRequestException($"Failed to get analysis result for operation '{operationId}': {response.StatusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analysis result for operation: {OperationId}", operationId);
            throw;
        }
    }

    /// <summary>
    /// Disposes resources
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
        // DefaultAzureCredential doesn't implement IDisposable
    }
}
