# Azure Content Understanding C# Sample Application

A comprehensive .NET 8 sample application demonstrating Azure Content Understanding capabilities with infrastructure as code deployment.

## 🚀 Features

- ✅ **Complete Azure Content Understa💾 Complete results saved to output files for detailed review.
📁 Results saved to:
    📄 Raw JSON: Output\receipt_069e39de-5132-425d-87b7_2025-08-07_09-46-09_results.json
    📋 Formatted: Output\receipt_069e39de-5132-425d-87b7_2025-08-07_09-46-09_formatted.txtg API integration** with authentication and URL constants
- ✅ **Enhanced polling system** with 20-minute timeout and progressive backoff for long-running operations
- ✅ **Improved JSON result parsing** with proper field extraction and error handling
- ✅ **Health checks for all Azure resources** (Content Understanding, Key Vault, Storage Account)
- ✅ **JSON-based analyzer schema management** with automatic discovery and validation
- ✅ **End-to-end document analysis pipeline** with real-time polling
- ✅ **Rich result formatting** with confidence levels and structured data display
- ✅ **Results export** to JSON and formatted text files with clean filename generation
- ✅ **Enhanced error handling** with operation timeout management and progressive retry logic
- ✅ **Parameterized CLI operations** with intelligent defaults and flexible file matching
- ✅ **Multi-format document support** (PDF, PNG, JPG, JPEG, TIFF, BMP)
- ✅ **Multiple execution modes** (interactive, health, analyzers, create, analyze)
- ✅ **Streamlined console output** with executive-level summaries
- ✅ **Comprehensive error handling** and user-friendly messages
- ✅ **Infrastructure as Code** with Terraform for one-click deployment

## 📋 Prerequisites

- **Azure Subscription** with appropriate permissions
- **.NET 8.0 SDK** or later
- **Terraform** (for infrastructure deployment)
- **Azure CLI** (for authentication and resource management)
- **PowerShell** (for Windows deployment scripts) or **Bash** (cross-platform)

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Azure Content Understanding              │
│                      C# Sample Application                  │
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────┴───────────────────────────────────────┐
│                   Application Layer                         │
│  ┌─────────────────┬─────────────────┬─────────────────┐    │
│  │  Console App    │   HTTP Service  │     Models      │    │
│  │                 │                 │                 │    │
│  │ • Interactive   │ • Authentication│ • Request/      │    │
│  │   Mode          │ • API Calls     │   Response      │    │
│  │ • Health Checks │ • Error Handling│   Objects       │    │
│  │ • CLI Support   │ • Result Polling│ • JSON Schemas  │    │
│  └─────────────────┴─────────────────┴─────────────────┘    │
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────┴───────────────────────────────────────┐
│                   Azure Resources                           │
│  ┌─────────────────┬─────────────────┬─────────────────┐    │
│  │  Content        │   Key Vault     │  Storage        │    │
│  │ Understanding   │                 │  Account        │    │
│  │                 │ • API Keys      │                 │    │
│  │ • Analyzers     │ • Secrets       │ • Sample Docs   │    │
│  │ • Document      │ • Managed       │ • Results       │    │
│  │   Analysis      │   Identity      │   Export        │    │
│  └─────────────────┴─────────────────┴─────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

## 🚀 Quick Start

### 1. Deploy Infrastructure

```powershell
# Windows (PowerShell)
cd iac
.\deploy.ps1 -SubscriptionId "your-subscription-id" -Location "eastus"

# Cross-platform (Bash)
cd iac
chmod +x deploy.sh
./deploy.sh
```

### 2. Build and Run

```bash
# Build the solution
dotnet build

# Run in interactive mode (default) - guided experience
dotnet run

# Or jump straight to specific operations
dotnet run -- --mode help                                    # See all options
dotnet run -- --mode health                                  # Health check
dotnet run -- --mode create-analyzer                         # Create analyzers
dotnet run -- --mode analyze --document receipt.png         # Analyze document
```

### 3. Verify Everything Works

The application will automatically:
1. ✅ Run health checks on all Azure resources
2. ✅ Show current status of analyzers and sample documents  
3. ✅ Guide you through next steps with intelligent defaults
4. ✅ Provide helpful suggestions when files or analyzers aren't found

## 📖 Usage Guide

### Command-Line Interface

The application supports multiple execution modes with flexible parameterization:

```bash
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

```bash
# Create default analyzer (receipt)
dotnet run -- --mode create-analyzer

# Create specific analyzer by file name
dotnet run -- --mode create-analyzer --analyzer-file receipt.json
dotnet run -- --mode create-analyzer --analyzer-file enginemanual

# The tool supports partial name matching and case-insensitive search
dotnet run -- --mode create --analyzer-file engine
```

### Document Analysis

```bash
# Use all defaults (receipt1.pdf + receipt analyzer)
dotnet run -- --mode analyze

# Specify document only (auto-detects best analyzer)
dotnet run -- --mode analyze --document receipt.png
dotnet run -- --mode analyze --document invoice.pdf

# Specify analyzer only (uses default document)
dotnet run -- --mode analyze --analyzer enginemanual

# Specify both analyzer and document for precise control
dotnet run -- --mode analyze --analyzer enginemanual --document V8721C_Instruction_manual.pdf
dotnet run -- --mode analyze --analyzer receipt --document receipt.png

# Use absolute paths for documents outside the project
dotnet run -- --mode analyze --document "C:\path\to\my\document.pdf"
```

### Supported File Formats

The application automatically detects content types for:
- **PDF**: `.pdf` (application/pdf)
- **Images**: `.png`, `.jpg`, `.jpeg`, `.tiff`, `.bmp`
- Automatic content-type header generation based on file extension

## 📋 CLI Reference

### Complete Command Syntax

```bash
dotnet run [-- --mode <mode>] [options]
```

### Available Modes

| Mode | Aliases | Description |
|------|---------|-------------|
| `help` | `--help`, `-h` | Show comprehensive help information |
| `health` | `healthcheck` | Run comprehensive health check of Azure resources |
| `analyzers` | `list` | List all available analyzers in the service |
| `create-analyzer` | `create` | Create analyzer from JSON schema files |
| `test-analysis` | `analyze` | Analyze documents with specified parameters |
| `interactive` | (default) | Interactive mode with guided menu |

### Available Options

| Option | Description | Used With |
|--------|-------------|-----------|
| `--analyzer-file <file>` | Specify analyzer JSON file (supports partial names) | `create-analyzer` |
| `--analyzer <name>` | Specify analyzer name for analysis | `analyze` |
| `--document <file>` | Specify document file (filename or absolute path) | `analyze` |

### Examples by Use Case

**Quick Start:**
```bash
dotnet run                    # Interactive guided experience
dotnet run -- --mode help    # See all options and examples
```

**Health & Status:**
```bash
dotnet run -- --mode health     # Check all Azure resources
dotnet run -- --mode analyzers  # List available analyzers
```

**Create Analyzers:**
```bash
dotnet run -- --mode create-analyzer                        # Create default
dotnet run -- --mode create --analyzer-file receipt         # Create receipt analyzer
dotnet run -- --mode create --analyzer-file enginemanual    # Create engine analyzer
```

**Document Analysis:**
```bash
# Simple analysis (uses defaults)
dotnet run -- --mode analyze

# Document-specific analysis
dotnet run -- --mode analyze --document receipt.png
dotnet run -- --mode analyze --document "C:\my\invoice.pdf"

# Analyzer-specific analysis  
dotnet run -- --mode analyze --analyzer enginemanual

# Full control
dotnet run -- --mode analyze --analyzer receipt --document receipt.png
```

### Sample Output

When running `dotnet run -- --mode analyze --document receipt.png`, you'll see the streamlined output:

```
🔍 Document analysis mode...

📄 Using document: receipt.png
📊 Document size: 723,344 bytes
🎯 Using specified analyzer: receipt
🧠 Analyzing document with analyzer: receipt
✅ Document analysis submitted successfully!
⏳ Polling for analysis results (timeout: 20 minutes, progressive backoff)...
🎉 Analysis completed successfully! (completed in 2 polling attempts, took 25 seconds)

📊 ANALYSIS SUMMARY:
===================
📈 Fields extracted: 2
🔑 Key data found:
  • VendorName: East Repair Inc. (98%)
  • Items: 3 items

💾 Complete results saved to output files for detailed review.
� Results saved to:
    📄 Raw JSON: Output\receipt_2025-08-07_09-46-09_results.json
    📋 Formatted: Output\receipt_2025-08-07_09-46-09_formatted.txt
```

**Advanced Example** - Analyzing engine manual with enginemanual analyzer:

```bash
dotnet run -- --mode analyze --analyzer enginemanual --document V8721C_Instruction_manual.pdf
```

Output:
```
📄 Using document: V8721C_Instruction_manual.pdf
📊 Document size: 3,686,008 bytes
� Using specified analyzer: enginemanual
🧠 Analyzing document with analyzer: enginemanual
🎉 Analysis completed successfully! (completed in 7 polling attempts)

📊 ANALYSIS SUMMARY:
===================
📈 Fields extracted: 15
🔑 Key data found:
  • VendorName: Engine Technologies Inc. (94%)
  • Items: 12 items

💾 Complete results saved to output files for detailed review.
```

## 📁 Project Structure

```
azure-ai-content-understanding-basic/
├── src/                                    # Source code
│   ├── ContentUnderstanding.Sample/       # Main console application
│   │   ├── Program.cs                     # Main entry point with parameterized CLI
│   │   ├── Services/                      # HTTP service layer
│   │   │   ├── ContentUnderstandingService.cs  # API client with auth
│   │   │   └── HealthCheckService.cs      # Health check implementation
│   │   ├── Data/                          # Analyzer schemas and sample documents
│   │   │   ├── SampleAnalyzers.cs         # JSON schema utilities
│   │   │   ├── receipt-Analyzer_*.json    # Receipt analyzer definition
│   │   │   ├── enginemanual-Analyzer_*.json  # Engine manual analyzer
│   │   │   └── SampleDocuments/           # Test PDF and image files
│   │   │       ├── receipt1.pdf           # Default sample receipt
│   │   │       ├── receipt.png            # Sample receipt image
│   │   │       ├── V8721C_Instruction_manual.pdf  # Engine manual sample
│   │   │       └── *.pdf                  # Additional sample documents
│   │   ├── Output/                        # Analysis results export (git-ignored)
│   │   │   ├── *_results.json             # Raw JSON results with timestamps
│   │   │   └── *_formatted.txt            # Human-readable formatted results
│   │   └── appsettings.json              # Configuration
│   └── ContentUnderstanding.Models/       # Cleaned analyzer definition models (2 essential classes)
│       └── ContentUnderstandingModels.cs  # AnalyzerDefinition & FieldDefinition only
├── iac/                                   # Infrastructure as Code (Terraform)
│   ├── main.tf                           # Main infrastructure definition
│   ├── variables.tf                      # Input parameters
│   ├── outputs.tf                        # Output values
│   ├── deploy.ps1                        # Windows deployment script
│   └── deploy.sh                         # Cross-platform deployment
├── docs/                                 # Documentation
│   ├── initial_plan.md                   # Project task list and status
│   └── CONFIGURATION.md                  # Configuration guide
├── .gitignore                            # Git ignore rules (includes Output/)
└── README.md                             # This file
```

## ⚙️ Configuration

### Required Settings

The application requires these configuration values in `appsettings.json`:

```json
{
  "AzureContentUnderstanding": {
    "Endpoint": "https://your-endpoint.cognitiveservices.azure.com/"
  },
  "AzureKeyVault": {
    "VaultUri": "https://your-keyvault.vault.azure.net/"
  },
  "AzureStorage": {
    "AccountName": "yourstorageaccount",
    "ContainerName": "samples"
  }
}
```

### Authentication

The application uses **DefaultAzureCredential** for authentication, supporting:
- Azure CLI (`az login`)
- Visual Studio credentials
- Environment variables
- Managed Identity (in Azure)

### Secret Management

- API keys are stored in Azure Key Vault
- The application retrieves the `ai-services-key` secret automatically
- No hardcoded credentials in source code
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

- [Quick Reference Guide](docs/QUICK_REFERENCE.md) - Command examples and troubleshooting
- [Configuration Guide](docs/CONFIGURATION.md) - Detailed setup and authentication  
- [Initial Project Plan](docs/initial_plan.md) - Development roadmap and task completion
- [Azure Content Understanding Documentation](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/)

## License

This project is licensed under the MIT License.
