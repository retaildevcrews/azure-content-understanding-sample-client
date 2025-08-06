# Azure Content Understanding C# Sample

A C# sample project demonstrating Azure Content Understanding capabilities with Infrastructure as Code deployment.

## Features

- **Analyzer Schema Management**: Add/update custom analyzer schemas to Content Understanding endpoints
- **Document Analysis**: Submit documents for analysis and retrieve results
- **Simple HTTP Approach**: Direct REST API calls for transparency and educational value
- **One-click Deployment**: Terraform-based infrastructure deployment
- **Key Vault Integration**: Secure secret management
- **Cross-platform**: PowerShell and Bash deployment scripts

## Project Structure

```
ContentUnderstandingSample/
├── src/
│   ├── ContentUnderstanding.Sample/          # Main console app with HTTP services
│   └── ContentUnderstanding.Models/          # Shared request/response models
├── iac/                                       # Infrastructure as Code (Terraform)
│   ├── main.tf
│   ├── variables.tf
│   ├── outputs.tf
│   ├── deploy.ps1                            # Windows deployment script
│   └── deploy.sh                             # Cross-platform deployment script
├── docs/                                      # Documentation
├── samples/                                   # Sample documents (user-provided)
└── README.md
```

## Prerequisites

- .NET 8 SDK
- Azure CLI
- Terraform
- Azure subscription with appropriate permissions

## Quick Start

1. **Deploy Infrastructure**:
   ```bash
   # Windows
   .\iac\deploy.ps1
   
   # Cross-platform
   ./iac/deploy.sh
   ```

2. **Run the Sample**:
   ```bash
   cd src/ContentUnderstanding.Sample
   dotnet run
   ```

## Configuration

The application uses `appsettings.json` for non-sensitive configuration and Azure Key Vault for secrets. Configuration is automatically set up during infrastructure deployment.

## Documentation

- [Initial Project Plan](docs/initial_plan.md)
- [Azure Content Understanding Documentation](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/)

## License

This project is licensed under the MIT License.
