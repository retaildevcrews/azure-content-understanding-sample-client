variable "resource_group_location" {
  type        = string
  description = "Location of the resource group."
  default     = "westus"
}

variable "resource_group_location_abbr" {
  type        = string
  description = "Abbreviation for Location of the resource group."
  default     = "wu"
}

variable "environment_name" {
  type        = string
  description = "The name of environment."
  default     = "dev"
}

variable "project_name" {
  type        = string
  description = "The name of the project."
  default     = "contentunderstanding"
}

variable "subscription_id" {
  type        = string
  description = "The subscription ID to use for the resources."
}

variable "deploy_ai_services" {
  type        = bool
  default     = true
  description = "Deploy Azure AI Services."
}

variable "enable_telemetry" {
  description = "Flag to enable telemetry for modules"
  type        = bool
  default     = false
}
