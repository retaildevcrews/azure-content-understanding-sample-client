#!/bin/bash

# Azure Content Understanding C# Sample - One-Click Deployment Script
# This script deploys the infrastructure using Terraform

set -e

echo "🚀 Starting deployment of Azure Content Understanding C# Sample..."

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Check if terraform is installed
if ! command_exists terraform; then
    echo "Installing Terraform..."
    # Detect architecture
    ARCH=$(uname -m)
    if [[ "$ARCH" == "x86_64" ]]; then
        TERRAFORM_ARCH="amd64"
    elif [[ "$ARCH" == "arm64" ]] || [[ "$ARCH" == "aarch64" ]]; then
        TERRAFORM_ARCH="arm64"
    else
        echo "❌ Unsupported architecture: $ARCH"
        exit 1
    fi
    
    # Download and install Terraform
    TERRAFORM_VERSION="1.5.0"
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        OS="linux"
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        OS="darwin"
    else
        echo "❌ Unsupported OS: $OSTYPE"
        exit 1
    fi
    
    curl -O "https://releases.hashicorp.com/terraform/${TERRAFORM_VERSION}/terraform_${TERRAFORM_VERSION}_${OS}_${TERRAFORM_ARCH}.zip"
    unzip "terraform_${TERRAFORM_VERSION}_${OS}_${TERRAFORM_ARCH}.zip"
    sudo mv terraform /usr/local/bin/
    rm "terraform_${TERRAFORM_VERSION}_${OS}_${TERRAFORM_ARCH}.zip"
    echo "✅ Terraform installed successfully"
fi

# Check Azure CLI authentication
echo "🔐 Checking Azure CLI authentication..."
if ! az account show &>/dev/null; then
    echo "❌ Not authenticated with Azure CLI."
    echo "🔑 Running 'az login' - please follow the authentication prompts..."
    az login
    
    # Verify authentication worked
    if ! az account show &>/dev/null; then
        echo "❌ Authentication failed. Please try again."
        exit 1
    fi
fi

# Get current subscription details
CURRENT_SUBSCRIPTION=$(az account show --query "id" -o tsv 2>/dev/null || echo "")
CURRENT_SUBSCRIPTION_NAME=$(az account show --query "name" -o tsv 2>/dev/null || echo "")
CURRENT_USER=$(az account show --query "user.name" -o tsv 2>/dev/null || echo "")

echo ""
echo "✅ Authenticated with Azure CLI"
echo "   Current subscription: $CURRENT_SUBSCRIPTION_NAME ($CURRENT_SUBSCRIPTION)"
echo "   Current user: $CURRENT_USER"
echo ""

# Prompt user for required parameters
echo "📝 Please provide the required deployment parameters:"
echo ""

# Get subscription ID with validation
while true; do
    echo ""
    echo "🔑 Please enter your Azure Subscription ID:"
    echo "   Current authenticated subscription: $CURRENT_SUBSCRIPTION_NAME ($CURRENT_SUBSCRIPTION)"
    echo "   (Format: 12345678-1234-1234-1234-123456789012)"
    printf "Subscription ID [default: current]: "
    
    read -r SUBSCRIPTION_ID
    
    # Use current subscription if empty and available
    if [[ -z "$SUBSCRIPTION_ID" ]] && [[ -n "$CURRENT_SUBSCRIPTION" ]]; then
        SUBSCRIPTION_ID="$CURRENT_SUBSCRIPTION"
    fi
    
    # Remove any whitespace
    SUBSCRIPTION_ID=$(echo "$SUBSCRIPTION_ID" | tr -d '[:space:]')
    
    if [[ -z "$SUBSCRIPTION_ID" ]]; then
        echo "❌ Subscription ID cannot be empty. Please try again."
    elif [[ ${#SUBSCRIPTION_ID} -ne 36 ]]; then
        echo "❌ Subscription ID must be exactly 36 characters. You entered ${#SUBSCRIPTION_ID} characters."
    elif [[ "$SUBSCRIPTION_ID" != *"-"* ]]; then
        echo "❌ Subscription ID must contain dashes. Please enter a valid GUID format."
    else
        echo "✅ Subscription ID accepted: $SUBSCRIPTION_ID"
        
        # Set the subscription if it's different from current
        if [[ "$SUBSCRIPTION_ID" != "$CURRENT_SUBSCRIPTION" ]]; then
            echo "🔄 Switching to subscription: $SUBSCRIPTION_ID"
            az account set --subscription "$SUBSCRIPTION_ID"
            if [[ $? -ne 0 ]]; then
                echo "❌ Failed to switch to subscription $SUBSCRIPTION_ID. Please check the subscription ID and ensure you have access."
                continue
            fi
            echo "✅ Successfully switched to subscription: $SUBSCRIPTION_ID"
        fi
        break
    fi
done

# Get resource group location
echo ""
echo "Available regions (Azure Content Understanding supported):"
echo "  1) westus (West US)"
echo "  2) swedencentral (Sweden Central)"
echo "  3) australiaeast (Australia East)"
echo ""
echo "⚠️  Note: Azure Content Understanding is available in these regions."
echo ""
while true; do
    echo ""
    printf "🌍 Select resource group location (1-3): "
    read -r LOCATION_CHOICE
    
    # Remove any whitespace
    LOCATION_CHOICE=$(echo "$LOCATION_CHOICE" | tr -d '[:space:]')
    
    case $LOCATION_CHOICE in
        1)
            RESOURCE_GROUP_LOCATION="westus"
            RESOURCE_GROUP_LOCATION_ABBR="wu"
            echo "✅ Selected: West US"
            break
            ;;
        2)
            RESOURCE_GROUP_LOCATION="swedencentral"
            RESOURCE_GROUP_LOCATION_ABBR="sc"
            echo "✅ Selected: Sweden Central"
            break
            ;;
        3)
            RESOURCE_GROUP_LOCATION="australiaeast"
            RESOURCE_GROUP_LOCATION_ABBR="ae"
            echo "✅ Selected: Australia East"
            break
            ;;
        *)
            echo "❌ Invalid choice '$LOCATION_CHOICE'. Please select 1, 2, or 3."
            ;;
    esac
done

# Get environment name
echo ""
while true; do
    printf "🏷️  Enter environment name [default: dev]: "
    read -r ENVIRONMENT_NAME
    
    # Use default if empty
    if [[ -z "$ENVIRONMENT_NAME" ]]; then
        ENVIRONMENT_NAME="dev"
    fi
    
    # Remove whitespace
    ENVIRONMENT_NAME=$(echo "$ENVIRONMENT_NAME" | tr -d '[:space:]')
    
    # Simple validation - check if it contains only alphanumeric characters
    if [[ "$ENVIRONMENT_NAME" =~ ^[a-zA-Z0-9]+$ ]]; then
        echo "✅ Environment: $ENVIRONMENT_NAME"
        break
    else
        echo "❌ Environment name must contain only letters and numbers."
    fi
done

# Get project name
echo ""
while true; do
    printf "📋 Enter project name [default: cusmple]: "
    read -r PROJECT_NAME
    
    # Use default if empty
    if [[ -z "$PROJECT_NAME" ]]; then
        PROJECT_NAME="cusmple"
    fi
    
    # Remove whitespace
    PROJECT_NAME=$(echo "$PROJECT_NAME" | tr -d '[:space:]')
    
    # Simple validation - check if it contains only alphanumeric characters
    if [[ "$PROJECT_NAME" =~ ^[a-zA-Z0-9]+$ ]]; then
        echo "✅ Project: $PROJECT_NAME"
        break
    else
        echo "❌ Project name must contain only letters and numbers."
    fi
done

echo ""
echo "✅ Configuration summary:"
echo "   - Subscription ID: ${SUBSCRIPTION_ID}"
echo "   - Location: ${RESOURCE_GROUP_LOCATION}"
echo "   - Location abbreviation: ${RESOURCE_GROUP_LOCATION_ABBR}"
echo "   - Environment: ${ENVIRONMENT_NAME}"
echo "   - Project: ${PROJECT_NAME}"
echo ""

# Change to the iac directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Initialize Terraform
echo "🏗️  Initializing Terraform..."
terraform init

# Plan the deployment
echo "📋 Planning deployment..."
terraform plan \
    -var="subscription_id=${SUBSCRIPTION_ID}" \
    -var="resource_group_location=${RESOURCE_GROUP_LOCATION}" \
    -var="resource_group_location_abbr=${RESOURCE_GROUP_LOCATION_ABBR}" \
    -var="environment_name=${ENVIRONMENT_NAME}" \
    -var="project_name=${PROJECT_NAME}"

# Ask for confirmation before applying
echo ""
echo "🤔 Do you want to proceed with the deployment? (y/N)"
printf "Enter your choice: "
read -r REPLY
echo ""
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "🚀 Deploying infrastructure..."
    terraform apply -auto-approve \
        -var="subscription_id=${SUBSCRIPTION_ID}" \
        -var="resource_group_location=${RESOURCE_GROUP_LOCATION}" \
        -var="resource_group_location_abbr=${RESOURCE_GROUP_LOCATION_ABBR}" \
        -var="environment_name=${ENVIRONMENT_NAME}" \
        -var="project_name=${PROJECT_NAME}"
    
    echo ""
    echo "🎉 Deployment completed successfully!"
    echo ""
    echo "📋 Next steps:"
    echo "   1. Run the C# sample application: 'cd ../src/ContentUnderstanding.Client && dotnet run'"
    echo "   2. Add your sample documents to the storage account"
    echo "   3. Create and test your analyzer schemas"
    echo ""
    echo "📖 For detailed instructions, see the README.md file"
else
    echo "❌ Deployment cancelled"
    exit 1
fi
