# Azure Content Understanding C# Sample - Windows Deployment Script
# This script deploys the infrastructure using Terraform

param(
    [string]$SubscriptionId = "",
    [string]$Location = "",
    [string]$Environment = "dev",
    [string]$ProjectName = "cusmple"
)

Write-Host "🚀 Starting deployment of Azure Content Understanding C# Sample..." -ForegroundColor Green

# Function to check if a command exists
function Test-CommandExists {
    param($Command)
    try {
        Get-Command $Command -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

# Check if Terraform is installed
if (-not (Test-CommandExists "terraform")) {
    Write-Host "❌ Terraform not found. Please install Terraform first." -ForegroundColor Red
    Write-Host "   Download from: https://www.terraform.io/downloads.html" -ForegroundColor Yellow
    exit 1
}

# Check if Azure CLI is installed
if (-not (Test-CommandExists "az")) {
    Write-Host "❌ Azure CLI not found. Please install Azure CLI first." -ForegroundColor Red
    Write-Host "   Download from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor Yellow
    exit 1
}

# Check Azure CLI authentication
Write-Host "🔐 Checking Azure CLI authentication..." -ForegroundColor Cyan
try {
    $currentAccount = az account show --query "id" -o tsv 2>$null
    if (-not $currentAccount) {
        throw "Not authenticated"
    }
}
catch {
    Write-Host "❌ Not authenticated with Azure CLI." -ForegroundColor Red
    Write-Host "🔑 Running 'az login' - please follow the authentication prompts..." -ForegroundColor Yellow
    az login
    
    # Verify authentication worked
    try {
        $currentAccount = az account show --query "id" -o tsv 2>$null
        if (-not $currentAccount) {
            throw "Authentication failed"
        }
    }
    catch {
        Write-Host "❌ Authentication failed. Please try again." -ForegroundColor Red
        exit 1
    }
}

# Get current subscription details
$currentSubscription = az account show --query "id" -o tsv 2>$null
$currentSubscriptionName = az account show --query "name" -o tsv 2>$null
$currentUser = az account show --query "user.name" -o tsv 2>$null

Write-Host ""
Write-Host "✅ Authenticated with Azure CLI" -ForegroundColor Green
Write-Host "   Current subscription: $currentSubscriptionName ($currentSubscription)" -ForegroundColor White
Write-Host "   Current user: $currentUser" -ForegroundColor White
Write-Host ""

# Get subscription ID if not provided
if ([string]::IsNullOrEmpty($SubscriptionId)) {
    do {
        Write-Host ""
        Write-Host "🔑 Please enter your Azure Subscription ID:" -ForegroundColor Cyan
        Write-Host "   Current authenticated subscription: $currentSubscriptionName ($currentSubscription)" -ForegroundColor White
        Write-Host "   (Format: 12345678-1234-1234-1234-123456789012)" -ForegroundColor Gray
        $userInput = Read-Host "Subscription ID [default: current]"
        
        if ([string]::IsNullOrEmpty($userInput) -and -not [string]::IsNullOrEmpty($currentSubscription)) {
            $SubscriptionId = $currentSubscription
        } else {
            $SubscriptionId = $userInput.Trim()
        }
        
        if ([string]::IsNullOrEmpty($SubscriptionId)) {
            Write-Host "❌ Subscription ID cannot be empty. Please try again." -ForegroundColor Red
        } elseif ($SubscriptionId.Length -ne 36) {
            Write-Host "❌ Subscription ID must be exactly 36 characters. You entered $($SubscriptionId.Length) characters." -ForegroundColor Red
        } elseif ($SubscriptionId -notmatch "-") {
            Write-Host "❌ Subscription ID must contain dashes. Please enter a valid GUID format." -ForegroundColor Red
        } else {
            Write-Host "✅ Subscription ID accepted: $SubscriptionId" -ForegroundColor Green
            
            # Set the subscription if it's different from current
            if ($SubscriptionId -ne $currentSubscription) {
                Write-Host "🔄 Switching to subscription: $SubscriptionId" -ForegroundColor Yellow
                az account set --subscription $SubscriptionId
                if ($LASTEXITCODE -ne 0) {
                    Write-Host "❌ Failed to switch to subscription $SubscriptionId. Please check the subscription ID and ensure you have access." -ForegroundColor Red
                    $SubscriptionId = ""
                    continue
                }
                Write-Host "✅ Successfully switched to subscription: $SubscriptionId" -ForegroundColor Green
            }
            break
        }
    } while ($true)
}

# Get location if not provided
if ([string]::IsNullOrEmpty($Location)) {
    Write-Host ""
    Write-Host "Available regions (Azure Content Understanding supported):" -ForegroundColor Cyan
    Write-Host "  1) westus (West US)" -ForegroundColor White
    Write-Host "  2) swedencentral (Sweden Central)" -ForegroundColor White
    Write-Host "  3) australiaeast (Australia East)" -ForegroundColor White
    Write-Host ""
    Write-Host "⚠️  Note: Azure Content Understanding is available in these regions." -ForegroundColor Yellow
    Write-Host ""
    
    do {
        $locationChoice = Read-Host "🌍 Select resource group location (1-3)"
        $locationChoice = $locationChoice.Trim()
        
        switch ($locationChoice) {
            "1" {
                $Location = "westus"
                $LocationAbbr = "wu"
                Write-Host "✅ Selected: West US" -ForegroundColor Green
                break
            }
            "2" {
                $Location = "swedencentral"
                $LocationAbbr = "sc"
                Write-Host "✅ Selected: Sweden Central" -ForegroundColor Green
                break
            }
            "3" {
                $Location = "australiaeast"
                $LocationAbbr = "ae"
                Write-Host "✅ Selected: Australia East" -ForegroundColor Green
                break
            }
            default {
                Write-Host "❌ Invalid choice '$locationChoice'. Please select 1, 2, or 3." -ForegroundColor Red
                $Location = ""
            }
        }
    } while ([string]::IsNullOrEmpty($Location))
}

# Get environment name
do {
    if ([string]::IsNullOrEmpty($Environment) -or $Environment -eq "dev") {
        $userInput = Read-Host "🏷️  Enter environment name [default: dev]"
        if ([string]::IsNullOrEmpty($userInput)) {
            $Environment = "dev"
        } else {
            $Environment = $userInput.Trim()
        }
    }
    
    if ($Environment -match "^[a-zA-Z0-9]+$") {
        Write-Host "✅ Environment: $Environment" -ForegroundColor Green
        break
    } else {
        Write-Host "❌ Environment name must contain only letters and numbers." -ForegroundColor Red
        $Environment = ""
    }
} while ([string]::IsNullOrEmpty($Environment))

# Get project name
do {
    if ([string]::IsNullOrEmpty($ProjectName) -or $ProjectName -eq "cusmple") {
        $userInput = Read-Host "📋 Enter project name [default: cusmple]"
        if ([string]::IsNullOrEmpty($userInput)) {
            $ProjectName = "cusmple"
        } else {
            $ProjectName = $userInput.Trim()
        }
    }
    
    if ($ProjectName -match "^[a-zA-Z0-9]+$") {
        Write-Host "✅ Project: $ProjectName" -ForegroundColor Green
        break
    } else {
        Write-Host "❌ Project name must contain only letters and numbers." -ForegroundColor Red
        $ProjectName = ""
    }
} while ([string]::IsNullOrEmpty($ProjectName))

Write-Host ""
Write-Host "✅ Configuration summary:" -ForegroundColor Green
Write-Host "   - Subscription ID: $SubscriptionId" -ForegroundColor White
Write-Host "   - Location: $Location" -ForegroundColor White
Write-Host "   - Location abbreviation: $LocationAbbr" -ForegroundColor White
Write-Host "   - Environment: $Environment" -ForegroundColor White
Write-Host "   - Project: $ProjectName" -ForegroundColor White
Write-Host ""

# Change to the iac directory
$scriptPath = $MyInvocation.MyCommand.Path
$iacPath = Split-Path -Parent $scriptPath
Set-Location $iacPath

# Initialize Terraform
Write-Host "🏗️  Initializing Terraform..." -ForegroundColor Cyan
terraform init

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Terraform initialization failed." -ForegroundColor Red
    exit 1
}

# Plan the deployment
Write-Host "📋 Planning deployment..." -ForegroundColor Cyan
terraform plan `
    -var="subscription_id=$SubscriptionId" `
    -var="resource_group_location=$Location" `
    -var="resource_group_location_abbr=$LocationAbbr" `
    -var="environment_name=$Environment" `
    -var="project_name=$ProjectName"

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Terraform planning failed." -ForegroundColor Red
    exit 1
}

# Ask for confirmation before applying
Write-Host ""
$confirmation = Read-Host "🤔 Do you want to proceed with the deployment? (y/N)"
Write-Host ""

if ($confirmation -match "^[Yy]$") {
    Write-Host "🚀 Deploying infrastructure..." -ForegroundColor Green
    terraform apply -auto-approve `
        -var="subscription_id=$SubscriptionId" `
        -var="resource_group_location=$Location" `
        -var="resource_group_location_abbr=$LocationAbbr" `
        -var="environment_name=$Environment" `
        -var="project_name=$ProjectName"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "🎉 Deployment completed successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📋 Next steps:" -ForegroundColor Cyan
    Write-Host "   1. Run the C# sample application: 'cd ../src/ContentUnderstanding.Client && dotnet run'" -ForegroundColor White
        Write-Host "   2. Add your sample documents to the storage account" -ForegroundColor White
        Write-Host "   3. Create and test your analyzer schemas" -ForegroundColor White
        Write-Host ""
        Write-Host "📖 For detailed instructions, see the README.md file" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Deployment failed." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "❌ Deployment cancelled" -ForegroundColor Red
    exit 1
}
