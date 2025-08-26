 # Azure Content Understanding C# Sample Application

A focused .NET 8 sample application demonstrating Azure Content Understanding capabilities with quick-start guidance and examples.

## 🚀 Features

- ✅ **Azure Content Understanding API integration** with authentication and configurable endpoints
- ✅ **Centralized polling** with 20-minute timeout (5s interval)
- ✅ **Improved JSON result parsing** with proper field extraction and error handling
- ✅ **Health checks for key Azure resources** (Content Understanding, Key Vault, Storage Account)
- ✅ **JSON-based analyzer schema management** with automatic discovery and validation
- ✅ **End-to-end document analysis pipeline** with real-time polling
- ✅ **Rich result formatting** with confidence levels and structured data display
- ✅ **Results export** to JSON and formatted text files with clean filename generation
- ✅ **Parameterized CLI operations** with sensible defaults
- ✅ **Multi-format document support** (PDF, PNG, JPG, JPEG, TIFF, BMP)

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

# Examples (non-interactive):
dotnet run -- --mode help
dotnet run -- --mode health
dotnet run -- --mode create-analyzer
dotnet run -- --mode analyze --document receipt.png
### Classify a whole directory

Classify all supported files in a subfolder under `Data/SampleDocuments` using a classifier:

```pwsh
dotnet run --project .\src\ContentUnderstanding.Client -- --mode classify-dir --classifier <name> --directory <subfolder>
```

- Non-recursive: only files directly in `<subfolder>` are processed.
- Supported types: .pdf, .png, .jpg, .jpeg, .tif, .tiff, .bmp.
- Sequential processing with per-file error logging; the run continues on errors.
- Outputs: per-file JSON and formatted text results in `Output/` plus a mandatory batch summary:
	- `batch_<directory>_<classifier>_<timestamp>_summary.json`

Example:

```pwsh
dotnet run --project .\src\ContentUnderstanding.Client -- --mode classify-dir --classifier products --directory receipts
```
```

## 📖 Usage Guide

### Command-Line Interface

The application supports multiple execution modes with flexible parameterization:

```powershell
# Interactive mode with guided menu (default)
dotnet run

# Show comprehensive help with examples
dotnet run -- --mode help

# Health check all Azure resources
dotnet run -- --mode health

# List all available analyzers
dotnet run -- --mode analyzers
```

### Creating Analyzers

```powershell
# Create default analyzer (receipt)
dotnet run -- --mode create-analyzer

# Create specific analyzer by file name
dotnet run -- --mode create-analyzer --analyzer-file receipt.json
```

### Document Analysis

```powershell
# Use all defaults (receipt1.pdf + receipt analyzer)
dotnet run -- --mode analyze

# Document-specific analysis
dotnet run -- --mode analyze --document receipt.png

# Analyzer-specific analysis
dotnet run -- --mode analyze --analyzer enginemanual

# Full control
dotnet run -- --mode analyze --analyzer receipt --document receipt.png

# Use absolute paths for documents outside the project
dotnet run -- --mode analyze --document "C:\\path\\to\\my\\document.pdf"
```

### Supported File Formats

The application automatically detects content types for:
- PDF: `.pdf` (application/pdf)
- Images: `.png`, `.jpg`, `.jpeg`, `.tiff`, `.bmp`

## 📋 CLI Reference

Complete command syntax:

```powershell
dotnet run [-- --mode <mode>] [options]
```

Available modes:

| Mode | Aliases | Description |
|------|---------|-------------|
| `help` | `--help`, `-h` | Show comprehensive help information |
| `health` | `healthcheck` | Run comprehensive health check of Azure resources |
| `analyzers` | `list` | List all available analyzers in the service |
| `create-analyzer` | `create` | Create analyzer from JSON schema files |
| `analyze` | `test-analysis` | Analyze documents with specified parameters |
| `interactive` | (default) | Interactive mode with guided menu |

Available options:

| Option | Description | Used With |
|--------|-------------|-----------|
| `--analyzer-file <file>` | Specify analyzer JSON file (supports partial names) | `create-analyzer` |
| `--analyzer <name>` | Specify analyzer name for analysis | `analyze` |
| `--document <file>` | Specify document file (filename or absolute path) | `analyze` |

## Examples by Use Case

**Quick Start:**
```powershell
# Interactive guided experience
dotnet run

# Show help
dotnet run -- --mode help
```

**Health & Status:**
```powershell
# Check configured Azure resources and connectivity
dotnet run -- --mode health

# List available analyzers
dotnet run -- --mode analyzers
```

**Create Analyzers:**
```powershell
# Create the default sample analyzer
dotnet run -- --mode create-analyzer

# Create from a specific analyzer JSON file (partial name allowed)
dotnet run -- --mode create-analyzer --analyzer-file receipt
```

**Document Analysis:**
```powershell
# Analyze using defaults (uses sample document + default analyzer)
dotnet run -- --mode analyze

# Analyze a specific document file
dotnet run -- --mode analyze --document receipt.png

# Use a specific analyzer and document
dotnet run -- --mode analyze --analyzer receipt --document receipt.png
```

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

## Document-only classification

This sample and CLI support document/binary classification only. Inline text classification (previously available as a `--text` option) has been removed. Use `--document <file>` to submit a document (PDF or image) for analysis or classification.

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

Note: in some environments Key Vault access may be restricted by network rules. For local development you can set the API key via environment variables or `appsettings.Development.json` if preferred.

## Documentation

- `docs/CONFIGURATION.md` - Configuration guide
- `docs/initial_plan.md` - Project plan and status
- Azure Content Understanding docs: https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/

## License

This project is licensed under the MIT License.
