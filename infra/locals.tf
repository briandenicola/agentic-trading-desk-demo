locals {
  # Reference-repo naming: random pet + random id, no static prefix. Names that
  # disallow dashes (ACR, Key Vault) are stripped and length-capped to limits.
  resource_name = "${random_pet.this.id}-${random_id.this.dec}"

  # Resource naming (all derive from the random resource_name token)
  resource_group_name = "${local.resource_name}-rg"
  acr_name            = substr(replace("${local.resource_name}acr", "-", ""), 0, 50)
  key_vault_name      = substr("${replace(local.resource_name, "-", "")}kv", 0, 24)
  uai_name            = "${local.resource_name}-id"
  log_workspace_name  = "${local.resource_name}-logs"
  app_insights_name   = "${local.resource_name}-appi"
  cae_name            = "${local.resource_name}-cae"
  ai_account_name     = "${local.resource_name}-ai"
  ai_project_name     = "${local.resource_name}-project"

  # Container app names
  ui_app_name            = "ui-app"
  orchestration_api_name = "orchestration-api"
  mock_api_name          = "mock-api"
  agent_provisioner_job  = "agent-provisioner-job"

  # Model deployment name (used as FOUNDRY_MODEL env var)
  model_deployment_name            = "${var.foundry_model}-deployment"
  morning_model_deployment_name    = "${var.foundry_morning_model}-deployment"
  specialist_model_deployment_name = "${var.foundry_specialist_model}-deployment"

  # Public HTTPS origin for the UI app. Derive from the Container Apps
  # environment domain to avoid a ui-app <-> orchestration-api dependency cycle.
  ui_app_origin = "https://${local.ui_app_name}.${azurerm_container_app_environment.main.default_domain}"

  # Common tags
  common_tags = merge(
    var.tags,
    {
      environment = var.environment
      location    = var.location
      app_name    = local.resource_name
    }
  )
}
