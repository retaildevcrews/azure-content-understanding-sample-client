output "resource_group_name" {
  description = "Name of the created resource group"
  value       = azurerm_resource_group.this.name
}

output "key_vault_name" {
  description = "Name of the created Key Vault"
  value       = azurerm_key_vault.this.name
}

output "key_vault_uri" {
  description = "URI of the created Key Vault"
  value       = azurerm_key_vault.this.vault_uri
}

output "ai_services_name" {
  description = "Name of the AI Services account"
  value       = var.deploy_ai_services ? module.ai_services[0].resource.name : null
}

output "ai_services_endpoint" {
  description = "Endpoint of the AI Services account"
  value       = var.deploy_ai_services ? module.ai_services[0].resource.endpoint : null
}

output "storage_account_name" {
  description = "Name of the storage account"
  value       = azurerm_storage_account.this.name
}

output "storage_container_name" {
  description = "Name of the storage container for samples"
  value       = azurerm_storage_container.samples.name
}

output "log_analytics_workspace_name" {
  description = "Name of the Log Analytics workspace"
  value       = azurerm_log_analytics_workspace.this.name
}

output "deployment_info" {
  description = "Important deployment information"
  value = {
    resource_group_name = azurerm_resource_group.this.name
    key_vault_name      = azurerm_key_vault.this.name
    key_vault_uri       = azurerm_key_vault.this.vault_uri
    ai_services_name    = var.deploy_ai_services ? module.ai_services[0].resource.name : null
    ai_services_endpoint = var.deploy_ai_services ? module.ai_services[0].resource.endpoint : null
    storage_account_name = azurerm_storage_account.this.name
    location            = azurerm_resource_group.this.location
  }
}
