# Configuration Guide

## Overview
This document explains how to configure the Azure Content Understanding C# Sample after deploying the infrastructure.

## Key Vault Secrets

The Terraform deployment automatically creates the following secrets in Azure Key Vault:

| Secret Name | Description | Usage |
|-------------|-------------|--------|
| `ai-services-key` | Azure AI Services API key for authentication | Used to authenticate API calls to Content Understanding service |

**Note**: Endpoint URLs and resource names are stored in appsettings.json as they are not sensitive information.

## Authentication Strategy

### Azure Content Understanding (AI Services)
- **Method**: API Key authentication (simpler) OR Managed Identity with RBAC (more secure)
- **Role**: `Cognitive Services User` (if using managed identity)
- **Authentication**: API key from Key Vault OR `DefaultAzureCredential`
- **Configuration**: Endpoint in appsettings.json, API key in Key Vault

### Azure Storage
- **Method**: Managed Identity with RBAC
- **Role**: `Storage Blob Data Contributor`
- **Authentication**: `DefaultAzureCredential`
- **Configuration**: Account name in appsettings.json
- **Access**: Read/write to sample documents container via managed identity

### Azure Key Vault
- **Method**: RBAC with `DefaultAzureCredential`
- **Role**: `Key Vault Administrator`
- **Access**: Read secrets for configuration

## Configuration Files

### appsettings.json
```json
{
  "AzureKeyVault": {
    "VaultUri": "https://[your-keyvault-name].vault.azure.net/"
  },
  "AzureContentUnderstanding": {
    "Endpoint": "https://[your-ai-services-name].cognitiveservices.azure.com/"
  },
  "AzureStorage": {
    "AccountName": "[your-storage-account-name]",
    "ContainerName": "samples"
  }
}
```

### appsettings.Development.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "ContentUnderstanding.Sample": "Debug"
    }
  },
  "AzureKeyVault": {
    "VaultUri": "https://[your-keyvault-name].vault.azure.net/"
  },
  "AzureContentUnderstanding": {
    "Endpoint": "https://[your-ai-services-name].cognitiveservices.azure.com/"
  },
  "AzureStorage": {
    "AccountName": "[your-storage-account-name]",
    "ContainerName": "samples"
  }
}
```

## Getting Configuration Values

After deploying with Terraform, get the configuration values:

```bash
# Get all deployment configuration (includes non-sensitive values)
terraform output deployment_info

# Example output:
# {
#   "ai_services_endpoint" = "https://your-ai-services-name.cognitiveservices.azure.com/"
#   "key_vault_uri" = "https://your-keyvault-name.vault.azure.net/"
#   "storage_account_name" = "yourstorageaccountname"
# }

# Update your appsettings.json with these values
```

## Health Check Endpoints

The application includes a health check that verifies:

1. **Key Vault Access** - Can authenticate and read secrets
2. **AI Services Access** - Can authenticate with managed identity
3. **Storage Access** - Can connect using connection string from Key Vault
4. **Configuration Validation** - All required settings are present

## Operation Polling Configuration

The application uses an enhanced polling system for long-running Content Understanding operations:

- **Polling Timeout**: 20 minutes maximum wait time
- **Progressive Backoff**: Starts at 10 seconds, gradually increases to 30 seconds maximum
- **Retry Logic**: Continues polling even if individual requests fail
- **Clean Results**: Operation IDs are cleaned for readable filenames
- **Export Format**: Results saved as both JSON and formatted text with operation ID included

### Polling Intervals
1. **Initial attempts (1-3)**: 10 seconds
2. **Mid-range attempts (4-8)**: 15 seconds  
3. **Later attempts (9-20)**: 20 seconds
4. **Final attempts (21+)**: 30 seconds (maximum)

## Troubleshooting

### Common Issues

1. **Key Vault Access Denied**
   - Ensure you're signed in with the same Azure account used for deployment
   - Verify RBAC permissions are correctly assigned
   - Check that `DefaultAzureCredential` can authenticate

2. **AI Services 403 Forbidden**
   - Verify API key is valid and not expired
   - Ensure AI Services endpoint URL is correct
   - Check that the service is deployed in the correct region
   - For managed identity: Verify `Cognitive Services User` role assignment

3. **Storage Access Issues**
   - Verify storage account name secret exists in Key Vault
   - Check `Storage Blob Data Contributor` role assignment
   - Ensure storage account allows Azure services bypass
   - Verify managed identity authentication is working

4. **Long-Running Operations**
   - Operations may take up to 15-20 minutes for complex documents
   - Application automatically handles timeout and retry logic
   - Check operation ID in Azure portal if polling times out
   - Use `--operation-id <id>` parameter to check specific operations
   - Look for results files in Output directory even if polling fails

### Debug Steps

1. Run the health check: `dotnet run --health-check`
2. Check Azure portal for RBAC assignments
3. Verify secrets exist in Key Vault
4. Test authentication with Azure CLI: `az account show`

## Security Best Practices

- **No hardcoded secrets** - All sensitive data stored in Key Vault
- **Managed Identity preferred** - Avoids key management for AI Services
- **RBAC permissions** - Principle of least privilege
- **DefaultAzureCredential** - Supports multiple auth methods (user, managed identity, service principal)
