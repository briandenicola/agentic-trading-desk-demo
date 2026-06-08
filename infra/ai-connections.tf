# RBAC propagation delay (research R4)
# Azure RBAC assignments take ~60-90 seconds to propagate to the control plane
# Without this delay, capability host creation fails with CapabilityHostOperationFailed
resource "time_sleep" "rbac_propagation" {
  count           = var.enable_foundry ? 1 : 0
  create_duration = "90s"
  depends_on = [
    azurerm_role_assignment.project_openai_user,
    azurerm_role_assignment.ai_project_manager
  ]
}

# Project connections (Azure OpenAI connection)
resource "azapi_resource" "project_connection" {
  count     = var.enable_foundry ? 1 : 0
  type      = "Microsoft.CognitiveServices/accounts/projects/connections@2025-06-01"
  name      = "azure-openai-connection"
  parent_id = azapi_resource.ai_project[0].id

  body = {
    properties = {
      category                    = "AzureOpenAI"
      target                      = azapi_resource.ai_account[0].id
      authType                    = "AAD"
      isSharedToAll               = true
      sharedUserList              = []
      useWorkspaceManagedIdentity = false
    }
  }

  depends_on = [time_sleep.rbac_propagation]
}

# Capability host (agent runtime) - MUST wait for RBAC propagation
# Schema validation disabled temporarily due to API versioning differences
resource "azapi_resource" "capability_host" {
  count                     = var.enable_foundry ? 1 : 0
  type                      = "Microsoft.CognitiveServices/accounts/projects/capabilityHosts@2025-06-01"
  name                      = "default"
  parent_id                 = azapi_resource.ai_project[0].id
  schema_validation_enabled = false

  body = {
    properties = {
      capabilityHostKind = "Agents"
    }
  }

  depends_on = [
    time_sleep.rbac_propagation,
    azapi_resource.project_connection
  ]
}
