variable "location" {
  description = "Azure region for resources"
  type        = string
  default     = "eastus"
}

variable "project_prefix" {
  description = "Prefix for resource naming (will be combined with random suffix)"
  type        = string
  default     = "wfgarage"
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "dev"
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default = {
    project    = "WF-Garage"
    managed_by = "terraform"
  }
}

# Foundry model configuration
# Defaults to gpt-4o-mini (2024-07-18) with GlobalStandard SKU
# This model+version is broadly available across Azure regions with favorable quota limits
variable "foundry_model" {
  description = "Azure OpenAI model name for Foundry project (e.g., gpt-4o-mini, gpt-4o)"
  type        = string
  default     = "gpt-4o-mini"
}

variable "foundry_model_version" {
  description = "Azure OpenAI model version (e.g., 2024-07-18)"
  type        = string
  default     = "2024-07-18"
}

variable "foundry_model_sku" {
  description = "Model SKU name (GlobalStandard for multi-region, Standard for single-region)"
  type        = string
  default     = "GlobalStandard"
}

variable "foundry_model_capacity" {
  description = "Model capacity (TPM in thousands). Start small (10-20) to avoid quota issues."
  type        = number
  default     = 10
}

# Runtime configuration
variable "demo_mode" {
  description = "Enable demo mode (1) or live mode (0) for API"
  type        = bool
  default     = true
}

variable "max_tool_hops" {
  description = "Maximum tool execution hops for agent orchestration"
  type        = number
  default     = 8
}
