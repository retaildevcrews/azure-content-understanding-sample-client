# Azure Content Understanding - Quick Reference

## 🚀 Quick Start Commands

```bash
# Interactive mode (recommended for first-time users)
dotnet run

# Health check all Azure resources
dotnet run -- --mode health

# Analyze with defaults (uses bundled sample document and default analyzer)
dotnet run -- --mode analyze

# Analyze specific document
dotnet run -- --mode analyze --document my-invoice.pdf

# Check specific operation status
dotnet run -- --mode check-operation --operation-id your-operation-id-here
```

## 📋 Common CLI Patterns

### Analyzer Management
```bash
# List all analyzers
dotnet run -- --mode analyzers

# Create analyzer from schema file
dotnet run -- --mode create-analyzer --analyzer receipt --analyzer-file receipt.json
```

### Document Analysis
```bash
# Quick analysis with smart defaults
dotnet run -- --mode analyze

# Specify document only (auto-picks analyzer)
dotnet run -- --mode analyze --document receipt.png
dotnet run -- --mode analyze --document "C:\path\to\document.pdf"

# Specify analyzer only (uses default document)
dotnet run -- --mode analyze --analyzer enginemanual

# Full control - specify both
dotnet run -- --mode analyze --analyzer receipt --document receipt.png
```

### Operation Management
```bash
# Check operation status
dotnet run -- --mode check-operation --operation-id 069e39de-5132-425d-87b7-9f84cd4317f5

# The system will automatically:
# - Poll for up to 20 minutes (5s interval)
# - Export results when complete
# - Handle timeouts gracefully
```

## ⚙️ Key Configuration Values

### Required in appsettings.json
```json
{
  "AzureContentUnderstanding": {
    "Endpoint": "https://your-endpoint.cognitiveservices.azure.com/"
  },
  "AzureKeyVault": {
    "VaultUri": "https://your-keyvault.vault.azure.net/"
  }
}
```

### Stored in Key Vault
- `ai-services-key` - Content Understanding API key

## 🔄 Operation Polling Details

- **Timeout**: 20 minutes maximum
- **Interval**: 5 seconds (fixed)
- **Retry Logic**: Continues even if individual requests fail
- **Export**: Results saved to Output/ directory when complete

## 📁 File Locations

```
project/
├── Data/SampleDocuments/           # Input documents
│   ├── receipt.png                # Default document
│   ├── receipt1.pdf              # Alternative receipt
│   └── V8721C_Instruction_manual.pdf  # Engine manual sample
├── Data/                          # Analyzer schemas
│   ├── receipt-Analyzer_*.json    # Receipt analyzer definition
│   └── enginemanual-Analyzer_*.json  # Engine manual analyzer
└── Output/                        # Results (auto-created)
    ├── *_results.json            # Raw API responses
    └── *_formatted.txt           # Human-readable results
```

## 🚨 Troubleshooting Quick Fixes

### "Operation never completed"
- Operations can take 15-20 minutes for complex documents
- Check Azure portal for operation status
- Use `--mode check-operation --operation-id <id>` to resume checking

### "Authentication failed"
- Run `az login` to ensure you're signed in
- Verify Key Vault access permissions
- Check that appsettings.json has correct endpoint URLs

### "Document not found"
- Use absolute paths: `--document "C:\full\path\to\file.pdf"`
- Verify file exists in Data/SampleDocuments/ for relative paths
- Supported formats: PDF, PNG, JPG, JPEG, TIFF, BMP

### "Analyzer not found"  
- Run `--mode analyzers` to see available analyzers
- Create analyzer first: `--mode create --analyzer-file receipt`
- Use exact analyzer names from the list

## 🔍 Sample Output Interpretation

```
📊 ANALYSIS SUMMARY:
===================
📈 Fields extracted: 2           # Number of fields found
🔑 Key data found:               # Top-level extracted data
  • VendorName: East Repair Inc. (98%)    # Field: Value (confidence%)
  • Items: 3 items                        # Array field with count

📁 Results saved to:
    📄 Raw JSON: Output\receipt_069e39de-5132-425d-87b7_2025-08-07_results.json
    📋 Formatted: Output\receipt_069e39de-5132-425d-87b7_2025-08-07_formatted.txt
```

- **Confidence levels**: 95%+ excellent, 80%+ good, below 80% review needed
- **Array fields**: Show count (e.g., "3 items") for arrays like line items
- **File naming**: `{document}_{operationId}_{timestamp}_{type}.{ext}`

## 💡 Pro Tips

1. **Start with Interactive Mode**: `dotnet run` for guided experience
2. **Check Health First**: `--mode health` to verify all connections
3. **Use Partial Names**: `--analyzer-file engine` works for "enginemanual-Analyzer_*.json"  
4. **Absolute Paths Work**: Use full file paths for documents outside the project
5. **Results Persist**: Check Output/ directory for previous analysis results
6. **Fixed Interval**: Polling uses a steady 5-second interval up to 20 minutes

## 📚 Related Documentation

- [Full README](../README.md) - Complete setup and architecture guide
- [Configuration Guide](CONFIGURATION.md) - Detailed configuration and authentication
- [Initial Plan](initial_plan.md) - Development roadmap and task completion status
