locals {
  # Generate unique suffix for resource naming
  suffix = "${random_pet.main.id}-${random_id.main.hex}"

  # Resource naming
  resource_group_name = "${var.project_prefix}-${var.environment}-rg"
  acr_name            = replace("${var.project_prefix}${var.environment}${random_id.main.hex}", "-", "")
  ai_account_name     = "${var.project_prefix}-${var.environment}-ai-${random_id.main.hex}"
  ai_project_name     = "${var.project_prefix}-${var.environment}-project"
  key_vault_name      = "${var.project_prefix}-${var.environment}-kv-${random_id.main.hex}"
  uai_name            = "${var.project_prefix}-${var.environment}-identity"
  log_workspace_name  = "${var.project_prefix}-${var.environment}-logs"
  app_insights_name   = "${var.project_prefix}-${var.environment}-ai-insights"
  cae_name            = "${var.project_prefix}-${var.environment}-cae"

  # Container app names
  ui_app_name            = "ui-app"
  orchestration_api_name = "orchestration-api"
  mock_api_name          = "mock-api"
  agent_provisioner_job  = "agent-provisioner-job"

  # Model deployment name (used as FOUNDRY_MODEL env var)
  model_deployment_name = "${var.foundry_model}-deployment"

  # Common tags
  common_tags = merge(
    var.tags,
    {
      environment = var.environment
      location    = var.location
    }
  )
}
