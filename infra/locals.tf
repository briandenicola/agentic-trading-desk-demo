locals {
  # Dynamic, collision-free naming: a single unique token (random_id) is applied
  # to every resource so repeated or parallel deployments never clash. Names that
  # disallow dashes (ACR, Key Vault) are stripped and length-capped to their limits.
  name_prefix = "${var.project_prefix}-${var.environment}"
  unique      = random_id.main.hex

  # Resource naming (all carry the unique token)
  resource_group_name = "${local.name_prefix}-rg-${local.unique}"
  acr_name            = substr(replace("${local.name_prefix}acr${local.unique}", "-", ""), 0, 50)
  key_vault_name      = substr(replace("${local.name_prefix}kv${local.unique}", "-", ""), 0, 24)
  uai_name            = "${local.name_prefix}-id-${local.unique}"
  log_workspace_name  = "${local.name_prefix}-logs-${local.unique}"
  app_insights_name   = "${local.name_prefix}-appi-${local.unique}"
  cae_name            = "${local.name_prefix}-cae-${local.unique}"
  ai_account_name     = "${local.name_prefix}-ai-${local.unique}"
  ai_project_name     = "${local.name_prefix}-project-${local.unique}"

  # Container app names
  ui_app_name            = "ui-app"
  orchestration_api_name = "orchestration-api"
  mock_api_name          = "mock-api"
  agent_provisioner_job  = "agent-provisioner-job"

  # Model deployment name (used as FOUNDRY_MODEL env var)
  model_deployment_name = "${var.foundry_model}-deployment"

  # Public HTTPS origin for the UI app. Derive from the Container Apps
  # environment domain to avoid a ui-app <-> orchestration-api dependency cycle.
  ui_app_origin = "https://${local.ui_app_name}.${azurerm_container_app_environment.main.default_domain}"

  # Common tags
  common_tags = merge(
    var.tags,
    {
      environment = var.environment
      location    = var.location
    }
  )
}
