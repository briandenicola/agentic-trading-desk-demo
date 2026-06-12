variable "location" {
  description = "Azure region for resources"
  type        = string
  default     = "eastus"
}

variable "environment" {
  description = "Environment label applied as a tag (demo, live) — not used in resource names"
  type        = string
  default     = "demo"
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default = {
    project    = "AgenticTradersDesk"
    managed_by = "terraform"
  }
}

# Foundry model configuration
# Defaults to gpt-4o-mini (2024-07-18) with GlobalStandard SKU
# This model+version is broadly available across Azure regions with favorable quota limits
variable "foundry_model" {
  description = "Azure OpenAI model name for Foundry project (e.g., gpt-5.4-mini, gpt-5.1)"
  type        = string
  default     = "gpt-5.4-mini"
}

variable "foundry_model_version" {
  description = "Azure OpenAI model version (e.g., 2026-03-17)"
  type        = string
  default     = "2026-03-17"
}

variable "foundry_model_sku" {
  description = "Model SKU name (GlobalStandard for multi-region, Standard for single-region)"
  type        = string
  default     = "GlobalStandard"
}

variable "foundry_model_capacity" {
  description = "Model capacity (TPM in thousands). 100 comfortably covers the RM-briefing agent's multi-tool-call token bursts; raise if you hit 429s."
  type        = number
  default     = 100
}

# --- Morning-brief synthesizer model (separate deployment = separate quota pool) ---
# The morning-brief agent runs on its own deployment so its token bursts never
# contend with the RM-briefing synthesizer or the fanned-out event specialists.
variable "foundry_morning_model" {
  description = "Azure OpenAI model for the morning-brief synthesizer agent (separate quota pool from foundry_model)."
  type        = string
  default     = "gpt-4o-mini"
}

variable "foundry_morning_model_version" {
  description = "Model version for the morning-brief synthesizer model."
  type        = string
  default     = "2024-07-18"
}

variable "foundry_morning_model_sku" {
  description = "SKU for the morning-brief synthesizer model deployment."
  type        = string
  default     = "GlobalStandard"
}

variable "foundry_morning_model_capacity" {
  description = "Capacity (TPM in thousands) for the morning-brief synthesizer model."
  type        = number
  default     = 50
}

# --- Event-specialist model (separate deployment = separate quota pool) ---
# The fan-out fires N specialists concurrently per brief, so it gets its own
# lightweight/fast model on a dedicated deployment to absorb the burst without 429s.
variable "foundry_specialist_model" {
  description = "Azure OpenAI model for the fanned-out event-specialist agent (separate quota pool; smallest/fastest is ideal for high-concurrency assessment)."
  type        = string
  default     = "gpt-5.4-nano"
}

variable "foundry_specialist_model_version" {
  description = "Model version for the event-specialist model."
  type        = string
  default     = "2026-03-17"
}

variable "foundry_specialist_model_sku" {
  description = "SKU for the event-specialist model deployment."
  type        = string
  default     = "GlobalStandard"
}

variable "foundry_specialist_model_capacity" {
  description = "Capacity (TPM in thousands) for the event-specialist model. Higher than the synthesizers because the fan-out is the high-concurrency path."
  type        = number
  default     = 100
}

# Foundry provisioning toggle
# When false, all Azure AI Foundry resources (AI account, model deployment,
# project, connections, capability host, and Foundry-scoped role assignments)
# are skipped. The container apps still receive the COMPUTED
# local.foundry_project_endpoint / local.model_deployment_name strings, which
# are never resolved at runtime because DEMO_MODE=1. Use enable_foundry=false
# for demo-only deployments.
variable "enable_foundry" {
  description = "Provision Azure AI Foundry resources (true) or skip them for demo-only deploys (false)"
  type        = bool
  default     = true
}

# Runtime configuration
variable "demo_mode" {
  description = "Enable demo mode (1) or live mode (0) for API"
  type        = bool
  default     = true
}

# Container Apps 3-step deploy gate.
# Step 1: apply with deploy_apps=false -> environment only (ACR, CAE, Key Vault,
#         identity, Foundry) so there is a registry to push images to.
# Step 2: `az acr build` pushes the application images.
# Step 3: apply with deploy_apps=true -> the Container Apps + job, which now have
#         real images to pull. This avoids the "manifest unknown" failure that
#         occurs when an app is created before its image exists.
variable "deploy_apps" {
  description = "Create the Container Apps + job (true) or only the supporting environment (false)"
  type        = bool
  default     = true
}

variable "max_tool_hops" {
  description = "Maximum tool execution hops for agent orchestration"
  type        = number
  default     = 8
}
