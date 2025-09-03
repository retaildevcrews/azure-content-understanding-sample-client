 # Azure Content Understanding C# Client Sample

A focused .NET 8 sample application demonstrating Azure Content Understanding capabilities with quick-start guidance and examples.

## Features

- **Terraform based iac** deploy/provision all necessary Azure resources
- **Azure Content Understanding API integration** with authentication and configurable endpoints
- **Health checks for key Azure resources** (Content Understanding, Key Vault)
- **Analyzer and Classifier samples** exploring several use cases
- **End-to-end document analysis pipeline**
  - Create/Upload Analayzers and Classifiers
  - Run analysis of single documents
  - Run classification and analysis on single/multiple documents
- **Simple CLI operations** with local export of results

## 📋 Prerequisites

- .NET 8 SDK
- (Optional) Azure CLI for authentication (`az login`)
- (Optional) Terraform if you want to deploy the sample infra in `iac/`

## 🚀 Quick Start

1) (Optional) Deploy infrastructure in `iac/` using the provided scripts.

2) Build and run the client

```powershell
# From the repo root
cd src/ContentUnderstanding.Client

# Build
dotnet build

# Run (interactive by default)
dotnet run

# Preferred: subcommands (System.CommandLine)
dotnet run -- --use-cli health
dotnet run -- --use-cli analyzers
dotnet run -- --use-cli analyze --analyzer receipt --document receipt1.pdf
dotnet run -- --use-cli classifiers
dotnet run -- --use-cli classify --classifier <name> --document <file>
dotnet run -- --use-cli classify-dir --classifier <name> --directory <subfolder>
### Classify a whole directory
```
Classify all supported files in a subfolder under `Data/SampleDocuments` using a classifier:

```pwsh
dotnet run --project .\src\ContentUnderstanding.Client -- --use-cli classify-dir --classifier <name> --directory <subfolder>
```

- Non-recursive: only files directly in `<subfolder>` are processed.
- Supported types: .pdf, .png, .jpg, .jpeg, .tif, .tiff, .bmp.
- Sequential processing with per-file error logging; the run continues on errors.
- Outputs: per-file JSON and formatted text results in `Output/` plus a mandatory batch summary:
	- `batch_<directory>_<classifier>_<timestamp>_summary.json`


## 📖 Usage Guide

### Command-Line Interface

Preferred: subcommands via System.CommandLine

```powershell
# Show comprehensive help
dotnet run -- --use-cli

# Health check core Azure resources (Content Understanding, Key Vault, Managed Identity)
dotnet run -- --use-cli health

# List all available analyzers
dotnet run -- --use-cli analyzers
```

### Creating Analyzers

```powershell
# Create default analyzer (receipt)
dotnet run -- --use-cli create-analyzer

# Create specific analyzer by file name
dotnet run -- --use-cli create-analyzer --analyzer-file receipt.json
```

### Document Analysis

```powershell
# Use all defaults (receipt1.pdf + receipt analyzer)
dotnet run -- --use-cli analyze

# Document-specific analysis
dotnet run -- --use-cli analyze --document receipt.png

# Analyzer-specific analysis
dotnet run -- --use-cli analyze --analyzer enginemanual

# Full control
dotnet run -- --use-cli analyze --analyzer receipt --document receipt.png

# Use absolute paths for documents outside the project
dotnet run -- --use-cli analyze --document "C:\\path\\to\\my\\document.pdf"
```

### Supported File Formats

The application automatically detects content types for:
- PDF: `.pdf` (application/pdf)
- Images: `.png`, `.jpg`, `.jpeg`, `.tiff`, `.bmp`

## 📋 CLI Reference

Preferred subcommands:

```powershell
dotnet run -- --use-cli <command> [options]
```

Common commands:

- health
- analyzers
- analyze --analyzer <name> --document <file>
- check-operation --operation-id <id>
- classifiers
- create-classifier --classifier <name> --classifier-file <file>
- create-analyzer --analyzer <name> --analyzer-file <file>
- classify --classifier <name> --document <file>
- classify-dir --classifier <name> --directory <subfolder>

## Project layout

```
azure-ai-content-understanding-basic/
├── src/
│   └── ContentUnderstanding.Client/       # Main console application (namespace ContentUnderstanding.Client)
│       ├── Program.cs                     # Main entry point with parameterized CLI
│       ├── Services/                      # HTTP service layer (ContentUnderstanding.Client.Services)
│       ├── Data/                          # Analyzer schemas and sample documents
│       ├── Models/                        # DTOs (ContentUnderstanding.Models namespace)
│       └── Output/                        # Analysis results export (git-ignored)
├── iac/                                   # Infrastructure as Code (Terraform)
├── docs/                                  # Documentation
└── README.md
```

## Configuration

The application uses `appsettings.json` for non-sensitive configuration and Azure Key Vault or environment variables for secrets. See `docs/CONFIGURATION.md` for details.

## Authentication

The application uses `DefaultAzureCredential` for authentication, supporting:
- Azure CLI (`az login`)
- Visual Studio credentials
- Environment variables
- Managed Identity (in Azure)

## Secret Management

- API keys are typically stored in Azure Key Vault
- The application looks for the `ai-services-key` secret by default
- No hardcoded credentials in source code

**Note**: Key Vault access may be restricted by network rules. For local development you can set the API key via environment variables or `appsettings.Development.json` if preferred.

## Documentation

- `docs/CONFIGURATION.md` - Configuration guide
- `docs/initial_plan.md` - Project plan and status
- Azure Content Understanding docs: https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/

## License

This project is licensed under the MIT License.
