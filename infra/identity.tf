data "azurerm_client_config" "current" {}

resource "azurerm_user_assigned_identity" "main" {
  name                = local.uai_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = local.common_tags
}

# ACR Pull role for container apps
resource "azurerm_role_assignment" "acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.main.principal_id
}

# Key Vault Secrets User for runtime config
resource "azurerm_role_assignment" "kv_secrets_user" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.main.principal_id
}

# Cognitive Services OpenAI User for Foundry model access
resource "azurerm_role_assignment" "openai_user" {
  scope                = azapi_resource.ai_account.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azurerm_user_assigned_identity.main.principal_id
}

# Azure AI Project Manager for Foundry project operations
resource "azurerm_role_assignment" "ai_project_manager" {
  scope                = azapi_resource.ai_project.id
  role_definition_name = "Azure AI Developer"
  principal_id         = azurerm_user_assigned_identity.main.principal_id
}
