using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ContentUnderstanding.Client.Services;

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

    // API Constants
    private const string API_BASE_PATH = "/contentunderstanding";
    private const string API_VERSION = "2025-05-01-preview";
    private const string ANALYZERS_PATH = "/analyzers";
    private const string OPERATIONS_PATH = "/operations";
    private const string ANALYZE_ACTION = ":analyze";
    private const string CLASSIFIERS_PATH = "/classifiers";
    private const string CLASSIFY_ACTION = ":classify";
    
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
    /// Polls an operation location until completion or timeout.
    /// Returns the final operation payload when status == "Succeeded".
    /// Throws TimeoutException on timeout and InvalidOperationException if the operation fails.
    /// </summary>
    public async Task<JsonDocument> PollResultAsync(
        string operationLocation,
        int timeoutSeconds = 1200,
        int pollingIntervalSeconds = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationLocation))
            throw new ArgumentException("Operation location cannot be null or empty", nameof(operationLocation));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var startTime = DateTime.UtcNow;
        while (!cts.IsCancellationRequested)
        {
            try
            {
                var responseText = await GetPolledDataAsync(operationLocation);
                var doc = JsonDocument.Parse(responseText);
                // Try to get status in a case-insensitive manner and support alternate key 'state'
                if (TryGetPropertyCaseInsensitive(doc.RootElement, "status", out var statusProp) ||
                    TryGetPropertyCaseInsensitive(doc.RootElement, "state", out statusProp))
                {
                    var status = statusProp.GetString();
                    if (string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(status, "Complete", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Analysis Succeeded after {Elapsed:mm\\:ss}", DateTime.UtcNow - startTime);
                        return doc;
                    }
                    if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                    {
                        string message = "Operation failed";
                        try
                        {
                            if (doc.RootElement.TryGetProperty("error", out var error))
                            {
                                var code = error.TryGetProperty("code", out var c) ? c.GetString() : null;
                                var msg = error.TryGetProperty("message", out var m) ? m.GetString() : null;
                                message = $"Operation failed. Code: {code ?? "Unknown"}, Message: {msg ?? "None"}";
                            }
                        }
                        catch { /* ignore detail extraction issues */ }
                        throw new InvalidOperationException(message);
                    }
                }

                // Still running or unknown status: wait and poll again
                string? statusForLog = null;
                if (TryGetPropertyCaseInsensitive(doc.RootElement, "status", out var s1)) statusForLog = s1.GetString();
                else if (TryGetPropertyCaseInsensitive(doc.RootElement, "state", out var s2)) statusForLog = s2.GetString();
                _logger.LogInformation("Polling... status: {Status}", statusForLog ?? "Unknown");
                if (statusForLog is null)
                {
                    // Provide a small payload snippet to aid troubleshooting
                    var snippet = responseText.Length > 512 ? responseText.Substring(0, 512) + "..." : responseText;
                    _logger.LogDebug("No status field detected in operation payload. Snippet: {Snippet}", snippet);
                }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Log and continue polling on transient errors
                _logger.LogWarning(ex, "Polling error; will retry in {DelaySeconds}s", pollingIntervalSeconds);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(pollingIntervalSeconds), cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                break;
            }
        }

        var opId = operationLocation.Split('/').LastOrDefault() ?? "unknown";
        throw new TimeoutException($"Polling timed out after {timeoutSeconds}s for operation {opId}.");
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
    /// Builds a complete API URL with consistent formatting
    /// </summary>
    private string BuildApiUrl(string path, bool includeApiVersion = true)
    {
        var endpoint = GetEndpoint();
        var url = $"{endpoint}{API_BASE_PATH}{path}";
        
        if (includeApiVersion)
        {
            var separator = path.Contains('?') ? "&" : "?";
            url += $"{separator}api-version={API_VERSION}";
        }
        
        return url;
    }

    /// <summary>
    /// Lists all analyzers, returning a slim JSON array of { analyzerId, description } objects.
    /// Accepts either a top-level array response or an object with a 'value' array.
    /// </summary>
    public async Task<string> ListAnalyzersAsync()
    {
        _logger.LogInformation("📋 Listing all analyzers...");
        try
        {
            await ConfigureAuthenticationAsync();
            var url = BuildApiUrl(ANALYZERS_PATH);
            _logger.LogDebug("GET {Url}", url);
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Failed to list analyzers. Status: {StatusCode}, Response: {Response}", response.StatusCode, content);
                throw new HttpRequestException($"Failed to list analyzers: {response.StatusCode} - {content}");
            }

            using var doc = JsonDocument.Parse(content);
            JsonElement arrayElem;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                arrayElem = doc.RootElement;
            }
            else if (doc.RootElement.TryGetProperty("value", out var valueProp) && valueProp.ValueKind == JsonValueKind.Array)
            {
                arrayElem = valueProp;
            }
            else
            {
                // Unexpected shape: return empty array to keep contract minimal and safe
                _logger.LogWarning("Analyzer list response had unexpected shape; returning empty list");
                return "[]";
            }

            var results = new List<Dictionary<string, string?>>();
            foreach (var item in arrayElem.EnumerateArray())
            {
                string? analyzerId = null;
                string? description = null;

                if (item.TryGetProperty("analyzerId", out var aId)) analyzerId = aId.GetString();
                else if (item.TryGetProperty("id", out var idProp)) analyzerId = idProp.GetString();

                if (item.TryGetProperty("description", out var descProp)) description = descProp.GetString();

                if (!string.IsNullOrWhiteSpace(analyzerId))
                {
                    results.Add(new Dictionary<string, string?>
                    {
                        ["analyzerId"] = analyzerId,
                        ["description"] = description
                    });
                }
            }

            _logger.LogInformation("✅ Successfully retrieved analyzers list (slimmed)");
            return JsonSerializer.Serialize(results);
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
            
            var url = BuildApiUrl($"{ANALYZERS_PATH}/{Uri.EscapeDataString(analyzerName)}");
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
            
            var url = BuildApiUrl($"{ANALYZERS_PATH}/{Uri.EscapeDataString(analyzerName)}");
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
            
            var url = BuildApiUrl($"{ANALYZERS_PATH}/{Uri.EscapeDataString(analyzerName)}");
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
    public async Task<(string responseContent, string operationLocation)> AnalyzeDocumentAsync(string analyzerName, byte[] documentData, string contentType)
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
            
            var url = BuildApiUrl($"{ANALYZERS_PATH}/{Uri.EscapeDataString(analyzerName)}{ANALYZE_ACTION}");
            _logger.LogDebug("POST {Url}", url);
            
            var requestContent = new ByteArrayContent(documentData);
            requestContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            
            var response = await _httpClient.PostAsync(url, requestContent);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully submitted document for analysis with analyzer: {AnalyzerName}", analyzerName);
                
                // Get the Operation-Location header for polling
                var operationLocation = response.Headers.Location?.ToString() ?? 
                                      (response.Headers.Contains("Operation-Location") ? 
                                       response.Headers.GetValues("Operation-Location").FirstOrDefault() : null);
                
                _logger.LogDebug("Operation-Location header: {OperationLocation}", operationLocation);
                
                return (responseContent, operationLocation ?? string.Empty);
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
    public async Task<string> GetOperationResultAsync(string operationId)
    {
        if (string.IsNullOrEmpty(operationId))
            throw new ArgumentException("Operation ID cannot be null or empty", nameof(operationId));

        _logger.LogInformation("📊 Getting operation result for: {OperationId}", operationId);
        
        try
        {
            await ConfigureAuthenticationAsync();
            
            var url = BuildApiUrl($"{OPERATIONS_PATH}/{Uri.EscapeDataString(operationId)}");
            _logger.LogDebug("GET {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully retrieved operation result for operation: {OperationId}", operationId);
                return responseContent;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("⚠️ Operation not found: {OperationId}", operationId);
                throw new InvalidOperationException($"Operation '{operationId}' not found");
            }
            else
            {
                _logger.LogError("❌ Failed to get operation result for operation {OperationId}. Status: {StatusCode}, Response: {Response}", 
                    operationId, response.StatusCode, responseContent);
                throw new HttpRequestException($"Failed to get operation result for operation '{operationId}': {response.StatusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting operation result for operation: {OperationId}", operationId);
            throw;
        }
    }

    /// <summary>
    /// Gets the result of a previously submitted analysis operation using the operation location URL
    /// </summary>
    public async Task<string> GetPolledDataAsync(string operationLocationUrl)
    {
        if (string.IsNullOrEmpty(operationLocationUrl))
            throw new ArgumentException("Operation location URL cannot be null or empty", nameof(operationLocationUrl));

        _logger.LogInformation("📊 Getting polled operation location: {OperationLocation}", operationLocationUrl);
        
        try
        {
            await ConfigureAuthenticationAsync();
            
            _logger.LogDebug("GET {Url}", operationLocationUrl);
            
            var response = await _httpClient.GetAsync(operationLocationUrl);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully retrieved polled location");
                return responseContent;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("⚠️ Operation not found at location: {OperationLocation}", operationLocationUrl);
                throw new InvalidOperationException($"Operation not found at location: {operationLocationUrl}");
            }
            else
            {
                _logger.LogError("❌ Failed to get polled location {OperationLocation}. Status: {StatusCode}, Response: {Response}", 
                    operationLocationUrl, response.StatusCode, responseContent);
                throw new HttpRequestException($"Failed to get polled location '{operationLocationUrl}': {response.StatusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting polled location: {OperationLocation}", operationLocationUrl);
            throw;
        }
    }

    // =========================
    // Classifiers (NEW)
    // =========================

    /// <summary>
    /// Lists all classifiers in the Content Understanding service
    /// </summary>
    public async Task<string> ListClassifiersAsync()
    {
        _logger.LogInformation("📋 Listing all classifiers...");
        try
        {
            await ConfigureAuthenticationAsync();
            var url = BuildApiUrl(CLASSIFIERS_PATH);
            _logger.LogDebug("GET {Url}", url);
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully retrieved classifiers list");
                return content;
            }
            else
            {
                _logger.LogError("❌ Failed to list classifiers. Status: {StatusCode}, Response: {Response}", response.StatusCode, content);
                throw new HttpRequestException($"Failed to list classifiers: {response.StatusCode} - {content}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing classifiers");
            throw;
        }
    }

    /// <summary>
    /// Gets a specific classifier by name
    /// </summary>
    public async Task<string> GetClassifierAsync(string classifierName)
    {
        if (string.IsNullOrEmpty(classifierName))
            throw new ArgumentException("Classifier name cannot be null or empty", nameof(classifierName));

        _logger.LogInformation("📄 Getting classifier: {ClassifierName}", classifierName);
        try
        {
            await ConfigureAuthenticationAsync();
            var url = BuildApiUrl($"{CLASSIFIERS_PATH}/{Uri.EscapeDataString(classifierName)}");
            _logger.LogDebug("GET {Url}", url);
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully retrieved classifier: {ClassifierName}", classifierName);
                return content;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("⚠️ Classifier not found: {ClassifierName}", classifierName);
                throw new InvalidOperationException($"Classifier '{classifierName}' not found");
            }
            else
            {
                _logger.LogError("❌ Failed to get classifier {ClassifierName}. Status: {StatusCode}, Response: {Response}", classifierName, response.StatusCode, content);
                throw new HttpRequestException($"Failed to get classifier '{classifierName}': {response.StatusCode} - {content}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting classifier: {ClassifierName}", classifierName);
            throw;
        }
    }

    /// <summary>
    /// Deletes a classifier if it exists
    /// </summary>
    public async Task DeleteClassifierAsync(string classifierName)
    {
        if (string.IsNullOrEmpty(classifierName))
            throw new ArgumentException("Classifier name cannot be null or empty", nameof(classifierName));

        _logger.LogInformation("🗑️ Deleting classifier: {ClassifierName}", classifierName);
        try
        {
            await ConfigureAuthenticationAsync();
            var url = BuildApiUrl($"{CLASSIFIERS_PATH}/{Uri.EscapeDataString(classifierName)}");
            _logger.LogDebug("DELETE {Url}", url);
            var response = await _httpClient.DeleteAsync(url);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully deleted classifier: {ClassifierName}", classifierName);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("⚠️ Classifier not found (already deleted?): {ClassifierName}", classifierName);
            }
            else
            {
                var text = await response.Content.ReadAsStringAsync();
                _logger.LogError("❌ Failed to delete classifier {ClassifierName}. Status: {StatusCode}, Response: {Response}", classifierName, response.StatusCode, text);
                throw new HttpRequestException($"Failed to delete classifier '{classifierName}': {response.StatusCode} - {text}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting classifier: {ClassifierName}", classifierName);
            throw;
        }
    }

    /// <summary>
    /// Creates or updates a classifier definition (JSON schema)
    /// </summary>
    public async Task<string> CreateOrUpdateClassifierAsync(string classifierName, string schemaJson)
    {
        if (string.IsNullOrEmpty(classifierName))
            throw new ArgumentException("Classifier name cannot be null or empty", nameof(classifierName));
        if (string.IsNullOrEmpty(schemaJson))
            throw new ArgumentException("Schema JSON cannot be null or empty", nameof(schemaJson));

        _logger.LogInformation("📝 Creating/updating classifier: {ClassifierName}", classifierName);
        try
        {
            await ConfigureAuthenticationAsync();
            var url = BuildApiUrl($"{CLASSIFIERS_PATH}/{Uri.EscapeDataString(classifierName)}");
            _logger.LogInformation(url);
            _logger.LogDebug("PUT {Url}", url);
            var content = new StringContent(schemaJson, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, content);
            var responseText = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully created/updated classifier: {ClassifierName}", classifierName);

                // Extract Operation ID from headers only (confirmed source)
                try
                {
                    string? opLocation = response.Headers.Location?.ToString()
                        ?? (response.Headers.Contains("Operation-Location")
                            ? response.Headers.GetValues("Operation-Location").FirstOrDefault()
                            : null);
                    if (!string.IsNullOrWhiteSpace(opLocation))
                    {
                        var tail = opLocation.Split('/').LastOrDefault();
                        var opId = CleanOperationId(tail);
                        if (!string.IsNullOrWhiteSpace(opId))
                        {
                            _logger.LogInformation("🆔 Operation ID (header) for create/update classifier: {OperationId}", opId);
                        }
                        else
                        {
                            _logger.LogInformation("ℹ️ Operation-Location header present but could not parse Operation ID");
                        }
                    }
                    else
                    {
                        _logger.LogInformation("ℹ️ No Operation-Location/Location header found for classifier create/update");
                    }
                }
                catch
                {
                    // Ignore extraction issues; operation ID may not be applicable for this API
                }

                return responseText;
            }
            else
            {
                _logger.LogError("❌ Failed to create/update classifier {ClassifierName}. Status: {StatusCode}, Response: {Response}", classifierName, response.StatusCode, responseText);
                throw new HttpRequestException($"Failed to create/update classifier '{classifierName}': {response.StatusCode} - {responseText}", null, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating classifier: {ClassifierName}", classifierName);
            throw;
        }
    }

    /// <summary>
    /// Submits content for classification using a classifier (binary content)
    /// Returns the raw response content and Operation-Location for polling (if provided)
    /// </summary>
    public async Task<(string responseContent, string operationLocation)> ClassifyAsync(string classifierName, byte[] contentData, string contentType)
    {
        if (string.IsNullOrEmpty(classifierName))
            throw new ArgumentException("Classifier name cannot be null or empty", nameof(classifierName));
        if (contentData == null || contentData.Length == 0)
            throw new ArgumentException("Content data cannot be null or empty", nameof(contentData));
        if (string.IsNullOrEmpty(contentType))
            throw new ArgumentException("Content type cannot be null or empty", nameof(contentType));

        _logger.LogInformation("🔎 Classifying content with classifier: {ClassifierName} (Size: {Size} bytes)", classifierName, contentData.Length);
        try
        {
            await ConfigureAuthenticationAsync();
            var url = BuildApiUrl($"{CLASSIFIERS_PATH}/{Uri.EscapeDataString(classifierName)}{CLASSIFY_ACTION}");
            _logger.LogDebug("POST {Url}", url);
            var requestContent = new ByteArrayContent(contentData);
            requestContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            var response = await _httpClient.PostAsync(url, requestContent);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Successfully submitted content for classification with classifier: {ClassifierName}", classifierName);
                var operationLocation = response.Headers.Location?.ToString() ??
                                        (response.Headers.Contains("Operation-Location")
                                            ? response.Headers.GetValues("Operation-Location").FirstOrDefault()
                                            : null);
                _logger.LogDebug("Operation-Location header: {OperationLocation}", operationLocation);
                return (responseContent, operationLocation ?? string.Empty);
            }
            else
            {
                _logger.LogError("❌ Failed to classify content with classifier {ClassifierName}. Status: {StatusCode}, Response: {Response}", classifierName, response.StatusCode, responseContent);
                throw new HttpRequestException($"Failed to classify content with classifier '{classifierName}': {response.StatusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying content with classifier: {ClassifierName}", classifierName);
            throw;
        }
    }

    // ClassifyTextAsync removed - document (binary) classification is the supported path

    /// <summary>
    /// Disposes resources
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
        // DefaultAzureCredential doesn't implement IDisposable
    }

    /// <summary>
    /// Tries to get a property from a JsonElement using case-insensitive name matching.
    /// </summary>
    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string name, out JsonElement property)
    {
        // Fast path: case-sensitive first
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out property))
            return true;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    property = prop.Value;
                    return true;
                }
            }
        }

        property = default;
        return false;
    }

    /// <summary>
    /// Normalizes an operation ID by removing any query strings and trailing segments after underscores.
    /// </summary>
    private static string? CleanOperationId(string? operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId)) return operationId;
        var clean = operationId.Split('?')[0];
        if (clean.Contains('_')) clean = clean.Split('_')[0];
        return clean;
    }
}
