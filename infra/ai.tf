locals {
  # Foundry project endpoint follows https://{account}.services.ai.azure.com/api/projects/{project}
  foundry_project_endpoint = "https://${local.ai_account_name}.services.ai.azure.com/api/projects/${local.ai_project_name}"
}

# Azure AI Services account (kind: AIServices, Foundry project management enabled)
resource "azapi_resource" "ai_account" {
  count                     = var.enable_foundry ? 1 : 0
  type                      = "Microsoft.CognitiveServices/accounts@2025-10-01-preview"
  name                      = local.ai_account_name
  parent_id                 = azurerm_resource_group.main.id
  location                  = azurerm_resource_group.main.location
  tags                      = local.common_tags
  schema_validation_enabled = false

  body = {
    kind = "AIServices"
    identity = {
      type = "SystemAssigned"
    }
    properties = {
      customSubDomainName    = local.ai_account_name
      publicNetworkAccess    = "Enabled"
      allowProjectManagement = true
    }
    sku = {
      name = "S0"
    }
  }

  response_export_values = ["properties.endpoint"]
}

# Model deployment within AI account
resource "azapi_resource" "model_deployment" {
  count     = var.enable_foundry ? 1 : 0
  type      = "Microsoft.CognitiveServices/accounts/deployments@2025-10-01-preview"
  name      = local.model_deployment_name
  parent_id = azapi_resource.ai_account[0].id

  body = {
    sku = {
      name     = var.foundry_model_sku
      capacity = var.foundry_model_capacity
    }
    properties = {
      model = {
        format  = "OpenAI"
        name    = var.foundry_model
        version = var.foundry_model_version
      }
    }
  }
}

# Azure AI Foundry project with SystemAssigned identity
resource "azapi_resource" "ai_project" {
  count                     = var.enable_foundry ? 1 : 0
  type                      = "Microsoft.CognitiveServices/accounts/projects@2025-10-01-preview"
  name                      = local.ai_project_name
  parent_id                 = azapi_resource.ai_account[0].id
  location                  = azurerm_resource_group.main.location
  tags                      = local.common_tags
  schema_validation_enabled = false

  identity {
    type = "SystemAssigned"
  }

  body = {
    properties = {
      displayName = "WF-Garage AI Project"
    }
  }

  depends_on = [azapi_resource.model_deployment]
}

# Role assignment for project identity (needed before capability host)
resource "azurerm_role_assignment" "project_openai_user" {
  count                = var.enable_foundry ? 1 : 0
  scope                = azapi_resource.ai_account[0].id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azapi_resource.ai_project[0].identity[0].principal_id
}
