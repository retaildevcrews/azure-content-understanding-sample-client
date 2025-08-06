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
        _credential = new DefaultAzureCredential();
    }

    /// <summary>
    /// Performs comprehensive health checks on all Azure resources
    /// </summary>
    /// <returns>Health check results with detailed status for each service</returns>
    public async Task<HealthCheckResult> CheckHealthAsync()
    {
        _logger.LogInformation("Starting comprehensive health check of Azure resources...");
        
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
            var serviceChecks = await Task.WhenAll(checks);
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

            _logger.LogInformation("Health check completed. Status: {Status}", result.OverallStatus);
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
    /// Checks Azure Content Understanding service accessibility
    /// </summary>
    private async Task<ServiceHealthCheck> CheckContentUnderstandingServiceAsync()
    {
        var check = new ServiceHealthCheck
        {
            ServiceName = "Azure Content Understanding",
            Status = "Unknown"
        };

        try
        {
            var endpoint = _configuration["AzureContentUnderstanding:Endpoint"];
            if (string.IsNullOrEmpty(endpoint))
            {
                check.Status = "Failed";
                check.Message = "Content Understanding endpoint not configured";
                check.Details = "Check appsettings.json or Key Vault for AzureContentUnderstanding:Endpoint";
                return check;
            }

            using var httpClient = new HttpClient();
            
            // Add authentication header
            var token = await _credential.GetTokenAsync(
                new Azure.Core.TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }));
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

            // Test basic connectivity with a simple GET request to the service
            var response = await httpClient.GetAsync($"{endpoint.TrimEnd('/')}/");
            
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // 404 is expected for root endpoint, indicates service is accessible
                check.Status = "Healthy";
                check.Message = "Service is accessible";
                check.Details = $"Endpoint: {endpoint}, Response: {response.StatusCode}";
            }
            else
            {
                check.Status = "Warning";
                check.Message = $"Unexpected response from service: {response.StatusCode}";
                check.Details = $"Endpoint: {endpoint}";
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
            var secretPages = client.GetPropertiesOfSecretsAsync();
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
            var testSecretName = "ai-services-endpoint";
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
    /// Checks Storage Account accessibility and container operations
    /// </summary>
    private async Task<ServiceHealthCheck> CheckStorageAccountAccessAsync()
    {
        var check = new ServiceHealthCheck
        {
            ServiceName = "Azure Storage Account",
            Status = "Unknown"
        };

        try
        {
            var storageAccountName = _configuration["AzureStorage:AccountName"];
            var containerName = _configuration["AzureStorage:ContainerName"] ?? "samples";

            if (string.IsNullOrEmpty(storageAccountName))
            {
                check.Status = "Failed";
                check.Message = "Storage account name not configured";
                check.Details = "Check appsettings.json for AzureStorage:AccountName";
                return check;
            }

            var blobServiceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");
            var blobServiceClient = new BlobServiceClient(blobServiceUri, _credential);
            
            // Test container access
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var containerExists = await containerClient.ExistsAsync();

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
                check.Message = "Storage account and container are accessible";
                check.Details = $"Account: {storageAccountName}, Container: {containerName}, Blobs: {blobCount}";
            }
            else
            {
                check.Status = "Warning";
                check.Message = "Storage account accessible but container not found";
                check.Details = $"Account: {storageAccountName}, Missing container: {containerName}";
            }
        }
        catch (Azure.Identity.AuthenticationFailedException ex)
        {
            check.Status = "Failed";
            check.Message = "Authentication failed for Storage Account";
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
                    var token = await _credential.GetTokenAsync(
                        new Azure.Core.TokenRequestContext(new[] { scope }));
                    
                    if (!string.IsNullOrEmpty(token.Token))
                    {
                        successfulScopes.Add(scope.Split('/')[2]); // Extract service name
                    }
                }
                catch
                {
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
        logger.LogInformation("=== HEALTH CHECK RESULTS ===");
        logger.LogInformation("Timestamp: {Timestamp}", Timestamp);
        logger.LogInformation("Overall Status: {Status}", OverallStatus);
        logger.LogInformation("Summary: {Summary}", Summary);
        logger.LogInformation("");

        foreach (var check in ServiceChecks)
        {
            var logLevel = check.Status switch
            {
                "Healthy" => LogLevel.Information,
                "Warning" => LogLevel.Warning,
                "Failed" => LogLevel.Error,
                _ => LogLevel.Information
            };

            logger.Log(logLevel, "🔍 {ServiceName}: {Status}", check.ServiceName, check.Status);
            logger.Log(logLevel, "   Message: {Message}", check.Message);
            if (!string.IsNullOrEmpty(check.Details))
            {
                logger.Log(logLevel, "   Details: {Details}", check.Details);
            }
            logger.LogInformation("");
        }
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
