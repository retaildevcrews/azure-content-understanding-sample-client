# Create a resource group
resource "azurerm_resource_group" "this" {
  location = var.resource_group_location
  name     = local.resource_group_name
  tags     = local.tags
}

# Create a random string for unique naming
resource "random_string" "unique" {
  length  = 4
  numeric = true
  special = false
  upper   = false
}

# Retrieve information about the current Azure client configuration
data "azurerm_client_config" "current" {}

# Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "this" {
  name                = "${local.log_analytics_name}${random_string.unique.result}"
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

# Key Vault
resource "azurerm_key_vault" "this" {
  name                = "${local.key_vault_name}${random_string.unique.result}"
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"
  
  enable_rbac_authorization = true
  purge_protection_enabled  = false
  
  tags = local.tags
}

# Key Vault Administrator role assignment for current user
resource "azurerm_role_assignment" "kv_admin" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Storage Account for sample documents
resource "azurerm_storage_account" "this" {
  name                     = "${local.storage_account_name}${random_string.unique.result}"
  resource_group_name      = azurerm_resource_group.this.name
  location                 = azurerm_resource_group.this.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  
  # Security settings
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = true
  
  tags = local.tags
}

# Storage container for sample documents
resource "azurerm_storage_container" "samples" {
  name                  = "samples"
  storage_account_name  = azurerm_storage_account.this.name
  container_access_type = "private"
}

# Azure AI Services (includes Content Understanding) - Using Azure Verified Module
module "ai_services" {
  count                         = var.deploy_ai_services ? 1 : 0
  source                        = "Azure/avm-res-cognitiveservices-account/azurerm"
  version                       = "0.7.1"
  resource_group_name           = azurerm_resource_group.this.name
  kind                          = "AIServices"
  name                          = "${local.ai_services_name}${random_string.unique.result}"
  custom_subdomain_name         = "${local.ai_services_name}${random_string.unique.result}"
  location                      = azurerm_resource_group.this.location
  enable_telemetry              = var.enable_telemetry
  sku_name                      = "S0"
  public_network_access_enabled = true
  
  # Enable managed identity - required for AI projects
  managed_identities = {
    system_assigned = true
  }
  
  local_auth_enabled                 = true
  outbound_network_access_restricted = false
  tags                              = local.tags
}

# Store AI Services endpoint and key in Key Vault
resource "azurerm_key_vault_secret" "ai_services_endpoint" {
  count        = var.deploy_ai_services ? 1 : 0
  name         = "ai-services-endpoint"
  value        = module.ai_services[0].resource.endpoint
  key_vault_id = azurerm_key_vault.this.id
  
  depends_on = [azurerm_role_assignment.kv_admin]
}

# Note: Commenting out AI Services Key secret temporarily
# We'll use Managed Identity instead of keys for better security
# resource "azurerm_key_vault_secret" "ai_services_key" {
#   count        = var.deploy_ai_services ? 1 : 0
#   name         = "ai-services-key"
#   value        = module.ai_services[0].private_keys.primary_key
#   key_vault_id = azurerm_key_vault.this.id

#   depends_on = [azurerm_role_assignment.kv_admin]
# }# Store storage connection string in Key Vault
resource "azurerm_key_vault_secret" "storage_connection_string" {
  name         = "storage-connection-string"
  value        = azurerm_storage_account.this.primary_connection_string
  key_vault_id = azurerm_key_vault.this.id
  
  depends_on = [azurerm_role_assignment.kv_admin]
}

# Cognitive Services User role assignment for current user
resource "azurerm_role_assignment" "ai_services_user" {
  count                = var.deploy_ai_services ? 1 : 0
  scope                = module.ai_services[0].resource_id
  role_definition_name = "Cognitive Services User"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Storage Blob Data Contributor role assignment for current user
resource "azurerm_role_assignment" "storage_contributor" {
  scope                = azurerm_storage_account.this.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = data.azurerm_client_config.current.object_id
}
