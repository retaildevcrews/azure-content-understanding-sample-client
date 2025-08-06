# Azure Content Understanding Infrastructure

This directory contains Terraform configuration files to deploy Azure infrastructure for the Content Understanding C# sample project.

## Architecture Overview

The infrastructure deploys:

- **Azure AI Services**: For Content Understanding capabilities
- **Azure Key Vault**: Secure storage for secrets and keys
- **Azure Storage Account**: For sample documents and data
- **Log Analytics Workspace**: Centralized logging and monitoring
- **Resource Group**: Container for all resources

## Prerequisites

1. **Azure CLI**: Install and authenticate
   ```bash
   az login
   az account set --subscription "your-subscription-id"
   ```

2. **Terraform**: Install Terraform >= 1.5.0
   ```bash
   # On macOS with Homebrew
   brew install terraform
   
   # On Windows with Chocolatey
   choco install terraform
   
   # Verify installation
   terraform version
   ```

3. **Permissions**: Ensure you have Contributor access to the Azure subscription

## Quick Start

### Option 1: One-Click Deployment (Recommended)

**Windows:**
```powershell
.\deploy.ps1
```

**Cross-platform:**
```bash
./deploy.sh
```

### Option 2: Manual Deployment

1. **Initialize Terraform**
   ```bash
   terraform init
   ```

2. **Configure Variables**
   Create your `terraform.tfvars` file:
   ```hcl
   subscription_id                = "your-subscription-id-here"
   resource_group_location        = "westus"
   resource_group_location_abbr   = "wu"
   environment_name              = "dev"
   project_name                  = "contentunderstanding"
   ```

3. **Plan and Deploy**
   ```bash
   # Review the deployment plan
   terraform plan
   
   # Deploy the infrastructure
   terraform apply
   ```

4. **Confirm Deployment**
   Type `yes` when prompted to confirm the deployment.

## Configuration Variables

### Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `subscription_id` | Azure subscription ID | "12345678-1234-..." |
| `resource_group_location` | Azure region for deployment | "westus" |
| `resource_group_location_abbr` | Region abbreviation for naming | "wu" |
| `environment_name` | Environment name | "dev" |
| `project_name` | Project identifier | "contentunderstanding" |

### Optional Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `deploy_ai_services` | Deploy Azure AI Services | `true` |
| `enable_telemetry` | Enable telemetry for modules | `false` |

## Supported Regions

Azure Content Understanding is available in:
- **westus** (West US)
- **swedencentral** (Sweden Central) 
- **australiaeast** (Australia East)

## Key Vault Secrets

The deployment automatically creates these secrets in Key Vault:

- `ai-services-endpoint`: Content Understanding service endpoint
- `ai-services-key`: Content Understanding service access key
- `storage-connection-string`: Storage account connection string

## Outputs

After deployment, Terraform outputs include:

- Resource group name
- Key Vault name and URI
- AI Services name and endpoint
- Storage account details

## Cleanup

To destroy all resources:

```bash
terraform destroy
```

**Warning**: This will permanently delete all resources and data. Ensure you have backups if needed.

## Troubleshooting

### Common Issues

1. **Subscription Access**: Ensure you have Contributor permissions
2. **Region Availability**: Use only supported Content Understanding regions
3. **Resource Naming**: Names must be globally unique (handled automatically with random suffix)
4. **Terraform State**: State is stored locally - be careful not to lose `terraform.tfstate`

### Support

For issues related to:
- **Infrastructure**: Check Terraform documentation
- **Azure Services**: Check Azure documentation
- **Sample Application**: See the main README.md

## Next Steps

After successful deployment:

1. Run the C# sample application
2. Upload sample documents to the storage account
3. Create and test analyzer schemas
4. Monitor application logs in Log Analytics
