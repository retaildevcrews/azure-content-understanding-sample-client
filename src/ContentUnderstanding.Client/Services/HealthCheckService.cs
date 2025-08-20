using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ContentUnderstanding.Sample.Services;

/// <summary>
/// Health check service to verify connectivity and access to deployed Azure resources
/// </summary>
public class HealthCheckService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly DefaultAzureCredential _credential;

    public HealthCheckService(IConfiguration configuration, ILogger<HealthCheckService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Use more timeout-friendly options for local development
        var options = new DefaultAzureCredentialOptions
        {
            ExcludeAzureCliCredential = false,
            ExcludeEnvironmentCredential = false,
            ExcludeManagedIdentityCredential = true, // Disable managed identity for local dev
            ExcludeVisualStudioCredential = false
        };
        _credential = new DefaultAzureCredential(options);
    }

    /// <summary>
    /// Performs comprehensive health checks on all Azure resources
    /// </summary>
    /// <returns>Health check results with detailed status for each service</returns>
    public async Task<HealthCheckResult> CheckHealthAsync()
    {
        _logger.LogInformation("🚀 Starting comprehensive health check of Azure resources...");
        
        var result = new HealthCheckResult
        {
            Timestamp = DateTime.UtcNow,
            OverallStatus = "Unknown"
        };

        var checks = new List<Task<ServiceHealthCheck>>
        {
            CheckContentUnderstandingServiceAsync(),
            CheckKeyVaultAccessAsync(),
            CheckStorageAccountAccessAsync(),
            CheckManagedIdentityAsync()
        };

        try
        {
            _logger.LogInformation("⏳ Running {Count} health checks (timeout: 2 minutes)...", checks.Count);
            
            // Add timeout to prevent hanging
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var serviceChecks = await Task.WhenAll(checks).WaitAsync(timeoutCts.Token);
            result.ServiceChecks = serviceChecks.ToList();

            // Determine overall status
            var failedChecks = serviceChecks.Count(c => c.Status == "Failed");
            var warningChecks = serviceChecks.Count(c => c.Status == "Warning");

            if (failedChecks > 0)
            {
                result.OverallStatus = "Failed";
                result.Summary = $"{failedChecks} service(s) failed, {warningChecks} warning(s)";
            }
            else if (warningChecks > 0)
            {
                result.OverallStatus = "Warning";
                result.Summary = $"All services accessible with {warningChecks} warning(s)";
            }
            else
            {
                result.OverallStatus = "Healthy";
                result.Summary = "All services are accessible and functioning correctly";
            }

            _logger.LogInformation("✅ Health check completed. Status: {Status}", result.OverallStatus);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Health check timed out after 2 minutes");
            result.OverallStatus = "Failed";
            result.Summary = "Health check timed out - one or more services took too long to respond";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed with exception");
            result.OverallStatus = "Failed";
            result.Summary = $"Health check failed: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Checks Azure Content Understanding service accessibility using API key from Key Vault
    /// </summary>
    private async Task<ServiceHealthCheck> CheckContentUnderstandingServiceAsync()
    {
        _logger.LogInformation("🧠 Checking Azure Content Understanding service...");
        
        var check = new ServiceHealthCheck
        {
            ServiceName = "Azure Content Understanding",
            Status = "Unknown"
        };

        try
        {
            // Get endpoint from configuration and API key from Key Vault
            var endpoint = _configuration["AzureContentUnderstanding:Endpoint"];
            if (string.IsNullOrEmpty(endpoint))
            {
                check.Status = "Failed";
                check.Message = "Content Understanding endpoint not configured";
                check.Details = "Configure AzureContentUnderstanding:Endpoint in appsettings.json";
                return check;
            }

            var keyVaultUri = _configuration["AzureKeyVault:VaultUri"];
            if (string.IsNullOrEmpty(keyVaultUri))
            {
                check.Status = "Failed";
                check.Message = "Key Vault URI not configured - cannot retrieve API key";
                check.Details = "Configure AzureKeyVault:VaultUri in appsettings.json";
                return check;
            }

            var keyVaultClient = new SecretClient(new Uri(keyVaultUri), _credential);
            string apiKey;
            
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var keySecret = await keyVaultClient.GetSecretAsync("ai-services-key", cancellationToken: cts.Token);
                apiKey = keySecret.Value.Value;
                _logger.LogDebug("Retrieved Content Understanding API key from Key Vault");
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                check.Status = "Failed";
                check.Message = "Content Understanding API key not found in Key Vault";
                check.Details = "Expected secret 'ai-services-key' in Key Vault (created by Terraform deployment)";
                return check;
            }

            using var httpClient = new HttpClient();
            
            // Set timeout to prevent hanging
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            // Add authentication header using API key
            httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

            // Test the analyzers endpoint to verify both connectivity and authentication
            var analyzersEndpoint = $"{endpoint.TrimEnd('/')}/contentunderstanding/analyzers?api-version=2025-05-01-preview";
            var response = await httpClient.GetAsync(analyzersEndpoint);
            
            if (response.IsSuccessStatusCode)
            {
                check.Status = "Healthy";
                check.Message = "Content Understanding service is accessible with API key authentication";
                check.Details = $"Endpoint: {endpoint}, Analyzers API responded with {response.StatusCode}";
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                check.Status = "Failed";
                check.Message = "Authentication failed - check API key validity";
                check.Details = $"Endpoint: {endpoint}, API key may be invalid or expired";
            }
            else
            {
                check.Status = "Warning";
                check.Message = $"Service responded with {response.StatusCode}";
                check.Details = $"Endpoint: {endpoint}, Response: {await response.Content.ReadAsStringAsync()}";
            }
        }
        catch (Azure.Identity.AuthenticationFailedException ex)
        {
            check.Status = "Failed";
            check.Message = "Authentication failed for Content Understanding service";
            check.Details = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            check.Status = "Failed";
            check.Message = "Network error accessing Content Understanding service";
            check.Details = ex.Message;
        }
        catch (Exception ex)
        {
            check.Status = "Failed";
            check.Message = "Unexpected error checking Content Understanding service";
            check.Details = ex.Message;
        }

        return check;
    }

    /// <summary>
    /// Checks Key Vault accessibility and secret retrieval
    /// </summary>
    private async Task<ServiceHealthCheck> CheckKeyVaultAccessAsync()
    {
        _logger.LogInformation("🔐 Checking Azure Key Vault access...");
        
        var check = new ServiceHealthCheck
        {
            ServiceName = "Azure Key Vault",
            Status = "Unknown"
        };

        try
        {
            var keyVaultUri = _configuration["AzureKeyVault:VaultUri"];
            if (string.IsNullOrEmpty(keyVaultUri))
            {
                check.Status = "Failed";
                check.Message = "Key Vault URI not configured";
                check.Details = "Check appsettings.json for AzureKeyVault:VaultUri";
                return check;
            }

            var client = new SecretClient(new Uri(keyVaultUri), _credential);
            
            // Test access by trying to list secrets (this doesn't retrieve values)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var secretPages = client.GetPropertiesOfSecretsAsync(cancellationToken: cts.Token);
            var secretCount = 0;
            
            await foreach (var secretProperty in secretPages)
            {
                secretCount++;
                if (secretCount >= 5) break; // Limit to first 5 for health check
            }

            check.Status = "Healthy";
            check.Message = "Key Vault is accessible";
            check.Details = $"URI: {keyVaultUri}, Found {secretCount} secret(s)";

            // Try to retrieve a specific secret if configured
            var testSecretName = "ai-services-key";
            try
            {
                var secret = await client.GetSecretAsync(testSecretName);
                check.Details += $", Successfully retrieved '{testSecretName}' secret";
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                check.Details += $", Secret '{testSecretName}' not found (expected if not deployed)";
            }
        }
        catch (Azure.Identity.AuthenticationFailedException ex)
        {
            check.Status = "Failed";
            check.Message = "Authentication failed for Key Vault";
            check.Details = ex.Message;
        }
        catch (Azure.RequestFailedException ex)
        {
            check.Status = "Failed";
            check.Message = $"Key Vault request failed: {ex.Status}";
            check.Details = ex.Message;
        }
        catch (Exception ex)
        {
            check.Status = "Failed";
            check.Message = "Unexpected error checking Key Vault";
            check.Details = ex.Message;
        }

        return check;
    }

    /// <summary>
    /// Checks Storage Account accessibility using managed identity authentication
    /// </summary>
    private async Task<ServiceHealthCheck> CheckStorageAccountAccessAsync()
    {
        _logger.LogInformation("💾 Checking Azure Storage Account access...");
        
        var check = new ServiceHealthCheck
        {
            ServiceName = "Azure Storage Account",
            Status = "Unknown"
        };

        try
        {
            // Get storage account name from configuration
            var storageAccountName = _configuration["AzureStorage:AccountName"];
            if (string.IsNullOrEmpty(storageAccountName))
            {
                check.Status = "Failed";
                check.Message = "Storage account name not configured";
                check.Details = "Configure AzureStorage:AccountName in appsettings.json";
                return check;
            }

            // Create BlobServiceClient using managed identity
            var storageUri = new Uri($"https://{storageAccountName}.blob.core.windows.net/");
            var blobServiceClient = new BlobServiceClient(storageUri, _credential);

            var containerName = _configuration["AzureStorage:ContainerName"] ?? "samples";
            
            // Test container access using managed identity
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var containerExists = await containerClient.ExistsAsync(cancellationToken: cts.Token);

            if (containerExists.Value)
            {
                // Try to list blobs in container
                var blobPages = containerClient.GetBlobsAsync();
                var blobCount = 0;
                
                await foreach (var blobItem in blobPages)
                {
                    blobCount++;
                    if (blobCount >= 5) break; // Limit for health check
                }

                check.Status = "Healthy";
                check.Message = "Storage account and container are accessible using managed identity";
                check.Details = $"Account: {blobServiceClient.AccountName}, Container: {containerName}, Blobs: {blobCount}";
            }
            else
            {
                check.Status = "Warning";
                check.Message = "Storage account accessible but container not found";
                check.Details = $"Account: {blobServiceClient.AccountName}, Missing container: {containerName}";
            }
        }
        catch (Azure.Identity.AuthenticationFailedException ex)
        {
            check.Status = "Failed";
            check.Message = "Authentication failed for Key Vault access";
            check.Details = ex.Message;
        }
        catch (Azure.RequestFailedException ex)
        {
            check.Status = "Failed";
            check.Message = $"Storage Account request failed: {ex.Status}";
            check.Details = ex.Message;
        }
        catch (Exception ex)
        {
            check.Status = "Failed";
            check.Message = "Unexpected error checking Storage Account";
            check.Details = ex.Message;
        }

        return check;
    }

    /// <summary>
    /// Checks managed identity authentication and token retrieval
    /// </summary>
    private async Task<ServiceHealthCheck> CheckManagedIdentityAsync()
    {
        _logger.LogInformation("🆔 Checking managed identity authentication...");
        
        var check = new ServiceHealthCheck
        {
            ServiceName = "Managed Identity",
            Status = "Unknown"
        };

        try
        {
            // Test managed identity by requesting tokens for different scopes
            var scopes = new[]
            {
                "https://cognitiveservices.azure.com/.default",
                "https://vault.azure.net/.default",
                "https://storage.azure.com/.default"
            };

            var successfulScopes = new List<string>();
            var failedScopes = new List<string>();

            foreach (var scope in scopes)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var token = await _credential.GetTokenAsync(
                        new Azure.Core.TokenRequestContext(new[] { scope }), cts.Token);
                    
                    if (!string.IsNullOrEmpty(token.Token))
                    {
                        successfulScopes.Add(scope.Split('/')[2]); // Extract service name
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Timeout getting token for scope: {Scope}", scope);
                    failedScopes.Add(scope.Split('/')[2] + " (timeout)");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Failed to get token for scope: {Scope}, Error: {Error}", scope, ex.Message);
                    failedScopes.Add(scope.Split('/')[2]);
                }
            }

            if (successfulScopes.Count == scopes.Length)
            {
                check.Status = "Healthy";
                check.Message = "Managed identity authentication successful for all services";
                check.Details = $"Successfully authenticated to: {string.Join(", ", successfulScopes)}";
            }
            else if (successfulScopes.Count > 0)
            {
                check.Status = "Warning";
                check.Message = "Managed identity partially working";
                check.Details = $"Success: {string.Join(", ", successfulScopes)}, Failed: {string.Join(", ", failedScopes)}";
            }
            else
            {
                check.Status = "Failed";
                check.Message = "Managed identity authentication failed for all services";
                check.Details = "Unable to obtain tokens for any Azure service scope";
            }
        }
        catch (Azure.Identity.AuthenticationFailedException ex)
        {
            check.Status = "Failed";
            check.Message = "Managed identity authentication failed";
            check.Details = ex.Message;
        }
        catch (Exception ex)
        {
            check.Status = "Failed";
            check.Message = "Unexpected error checking managed identity";
            check.Details = ex.Message;
        }

        return check;
    }
}

/// <summary>
/// Overall health check result
/// </summary>
public class HealthCheckResult
{
    public DateTime Timestamp { get; set; }
    public string OverallStatus { get; set; } = "Unknown";
    public string Summary { get; set; } = "";
    public List<ServiceHealthCheck> ServiceChecks { get; set; } = new();

    public void DisplayResults(ILogger logger)
    {
        logger.LogInformation("==========================================");
        logger.LogInformation("🏥 HEALTH CHECK RESULTS");
        logger.LogInformation("==========================================");
        logger.LogInformation("⏰ Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss} UTC", Timestamp);
        
        var statusEmoji = OverallStatus switch
        {
            "Healthy" => "✅",
            "Warning" => "⚠️",
            "Failed" => "❌",
            _ => "❓"
        };
        
        logger.LogInformation("📊 Overall Status: {Emoji} {Status}", statusEmoji, OverallStatus);
        logger.LogInformation("📝 Summary: {Summary}", Summary);
        logger.LogInformation("");
        logger.LogInformation("🔍 Individual Service Results:");
        logger.LogInformation("------------------------------------------");

        foreach (var check in ServiceChecks)
        {
            var emoji = check.Status switch
            {
                "Healthy" => "✅",
                "Warning" => "⚠️",
                "Failed" => "❌",
                _ => "❓"
            };
            
            var logLevel = check.Status switch
            {
                "Healthy" => LogLevel.Information,
                "Warning" => LogLevel.Warning,
                "Failed" => LogLevel.Error,
                _ => LogLevel.Information
            };

            logger.Log(logLevel, "{Emoji} {ServiceName}: {Status}", emoji, check.ServiceName, check.Status);
            logger.Log(logLevel, "   📄 {Message}", check.Message);
            if (!string.IsNullOrEmpty(check.Details))
            {
                logger.Log(logLevel, "   🔎 {Details}", check.Details);
            }
            logger.LogInformation("");
        }
        
        logger.LogInformation("==========================================");
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}

/// <summary>
/// Health check result for an individual service
/// </summary>
public class ServiceHealthCheck
{
    public string ServiceName { get; set; } = "";
    public string Status { get; set; } = "Unknown"; // Healthy, Warning, Failed, Unknown
    public string Message { get; set; } = "";
    public string Details { get; set; } = "";
}
