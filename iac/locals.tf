locals {
  # Resource naming convention
  resource_postfix    = "${var.environment_name}${var.project_name}${var.resource_group_location_abbr}"
  resource_group_name = "rg${local.resource_postfix}"
  
  # Individual resource names
  key_vault_name = "kv${local.resource_postfix}"
  ai_services_name = "ais${local.resource_postfix}"
  storage_account_name = lower("sa${local.resource_postfix}")
  log_analytics_name = "log${local.resource_postfix}"
  
  # Common tags
  tags = {
    Environment = var.environment_name
    Project     = var.project_name
    ManagedBy   = "Terraform"
    Purpose     = "ContentUnderstanding"
  }
}
