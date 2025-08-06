# Azure Content Understanding C# Sample Project - Task List

## Project Overview
Build a C# sample project to test Azure Content Understanding capabilities with infrastructure as code deployment. The sample will demonstrate:
- Adding/updating analyzer schema to Content Understanding endpoint
- Sending files for analysis
- Retrieving and displaying results
- One-click deployment of Azure resources

## Task List

### Phase 1: Project Setup and Planning

- [x] **Initialize Project Structure in the current workspace**
  - [x] Create solution structure with separate projects for:
    - [x] Console application (main sample with HTTP service classes)
    - [x] Shared models library for request/response objects
    - [x] Infrastructure as Code (terraform, following the code in https://github.com/Azure-Samples/data-extraction-using-azure-content-understanding/tree/main/iac)
    - [x] Documentation

- [x] **Set up Development Environment in the current workspace**
  - [x] Configure .NET project (target .NET 8 or latest LTS)
  - [x] Add necessary NuGet packages:
    - [x] `Azure.Identity` for authentication (DefaultAzureCredential)
    - [x] `Microsoft.Extensions.Configuration`
    - [x] `Microsoft.Extensions.Logging`
    - [x] `System.Text.Json` for JSON handling
    - [x] `Azure.Security.KeyVault.Secrets` for Key Vault integration

### Phase 2: Infrastructure as Code (IaC)

- [x] **Develop terraform** (following the referenced GitHub sample pattern)
  - [x] Content Understanding service provisioning
  - [x] Storage account for sample files
  - [x] Key Vault for secrets management
  - [x] Managed Identity setup
  - [x] Output necessary connection strings and endpoints

- [x] **Create Deployment Scripts**
  - [x] PowerShell script for one-click deployment (Windows)
  - [x] Bash script for cross-platform deployment (following Azure sample pattern)
  - [x] Azure CLI deployment commands
  - [x] Environment variable setup
  - [x] Post-deployment configuration script

- [x] **Add Parameter Files**
  - [x] Development environment parameters
  - [x] Production environment parameters
  - [x] Default configuration values

### Phase 3: Core Application Development

- [ ] **Implement HTTP Service Classes**
  - [ ] Create simple HTTP service class for Content Understanding API calls
  - [ ] Implement authentication using DefaultAzureCredential
  - [ ] Add methods for direct REST API calls:
    - [ ] Adding/updating analyzer schema
    - [ ] Submitting files for analysis
    - [ ] Retrieving analysis results
    - [ ] Simple error handling (pass-through server responses)

- [ ] **Create Request/Response Models**
  - [ ] Define simple model classes for API requests and responses
  - [ ] JSON serialization attributes for Content Understanding format
  - [ ] Keep models lightweight and focused on the specific operations needed

- [ ] **Create Sample Analyzer Schema**
  - [ ] Define a sample document type (e.g., invoice, receipt, or form)
  - [ ] Create JSON schema following Azure Content Understanding format
  - [ ] Include field definitions and extraction rules

- [ ] **Implement Main Application Logic**
  - [ ] Configuration management (appsettings.json + Key Vault for secrets)
  - [ ] Logging setup
  - [ ] Main workflow implementation:
    - [ ] Load and validate analyzer schema
    - [ ] Upload/update schema to Content Understanding endpoint
    - [ ] Select sample document for analysis
    - [ ] Submit document for processing
    - [ ] Poll for completion and retrieve results
    - [ ] Display extracted data in readable format

### Phase 4: Sample Data and Testing

- [ ] **Create Sample Documents - will be provided by author**
  - [ ] Add sample PDF/image files to test with
  - [ ] Include various document types that match your analyzer schema
  - [ ] Store in project resources or separate data folder

- [ ] **Implement Error Handling and Validation**
  - [ ] Basic HTTP status code handling for API calls
  - [ ] Pass-through server error responses for transparency
  - [ ] Validate configuration and required parameters
  - [ ] Create meaningful error messages for users

### Phase 5: Documentation and User Experience

- [ ] **Create Documentation**
  - [ ] README.md with:
    - [ ] Project overview and capabilities
    - [ ] Prerequisites and setup instructions
    - [ ] One-click deployment guide
    - [ ] Usage examples
    - [ ] Troubleshooting section
  - [ ] Code documentation and XML comments
  - [ ] Architecture diagram

- [ ] **Add User-Friendly Features**
  - [ ] Command-line argument support for different scenarios
  - [ ] Interactive mode for selecting documents
  - [ ] Progress indicators for long-running operations
  - [ ] Results export to JSON/CSV format

### Phase 6: Deployment and Testing

- [ ] **Create Deployment Package**
  - [ ] PowerShell script for complete setup (Windows):
    - [ ] Azure resource deployment
    - [ ] Application configuration
    - [ ] Sample data upload
    - [ ] Initial test run
  - [ ] Bash script for cross-platform deployment (following Azure sample pattern)

- [ ] **End-to-End Testing**
  - [ ] Test complete deployment from scratch
  - [ ] Validate all Content Understanding operations
  - [ ] Test with different document types
  - [ ] Verify cleanup procedures

### Phase 7: Advanced Features (Optional)

- [ ] **Add Advanced Capabilities**
  - [ ] Batch processing support
  - [ ] Multiple analyzer schema management
  - [ ] Results comparison and validation
  - [ ] Performance metrics and timing

- [ ] **CI/CD Integration**
  - [ ] GitHub Actions workflow for testing
  - [ ] Automated deployment pipeline
  - [ ] Infrastructure validation tests

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

## References

- [Azure Content Understanding Tutorial](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/tutorial/create-custom-analyzer)
- [Azure Samples IaC Reference](https://github.com/Azure-Samples/data-extraction-using-azure-content-understanding/tree/main/iac)
- [Azure Samples Deployment Script Reference](https://github.com/Azure-Samples/data-extraction-using-azure-content-understanding/blob/main/deploy.sh)

## Notes

- Target .NET 8 or latest LTS version
- Use DefaultAzureCredential for simple, current-user authentication
- Follow Azure best practices for Key Vault integration
- Keep HTTP calls direct and transparent (similar to Python tutorial approach)
- Ensure one-click deployment capability with Terraform
- Include comprehensive logging but simple error handling
- Create clear documentation for easy adoption

---

**Created:** August 6, 2025  
**Status:** Planning Phase  
**Next Steps:** Begin Phase 1 - Project Setup and Planning