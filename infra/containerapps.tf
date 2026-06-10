resource "azurerm_container_app_environment" "main" {
  name                       = local.cae_name
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.common_tags
}

# UI App (external ingress)
resource "azurerm_container_app" "ui_app" {
  count                        = var.deploy_apps ? 1 : 0
  name                         = local.ui_app_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = local.common_tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.main.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.main.id
  }

  template {
    container {
      name = "ui-app"
      # Placeholder image - CD pipeline updates this via `az containerapp update`
      image  = "${azurerm_container_registry.main.login_server}/ui-app:latest"
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "ORCHESTRATION_API_URL"
        value = "https://${azurerm_container_app.orchestration_api[0].ingress[0].fqdn}"
      }
    }

    min_replicas = 1
    max_replicas = 3
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  lifecycle {
    ignore_changes = [
      template[0].container[0].image
    ]
  }

  depends_on = [
    azurerm_role_assignment.acr_pull
  ]
}

# Orchestration API (internal ingress)
resource "azurerm_container_app" "orchestration_api" {
  count                        = var.deploy_apps ? 1 : 0
  name                         = local.orchestration_api_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = local.common_tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.main.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.main.id
  }

  template {
    container {
      name   = "orchestration-api"
      image  = "${azurerm_container_registry.main.login_server}/orchestration-api:latest"
      cpu    = 0.5
      memory = "1.0Gi"

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      env {
        name  = "DEMO_MODE"
        value = var.demo_mode ? "1" : "0"
      }

      env {
        name  = "MOCK_API_BASEURL"
        value = "https://${azurerm_container_app.mock_api[0].ingress[0].fqdn}"
      }

      env {
        name  = "CORS_ALLOWED_ORIGINS"
        value = local.ui_app_origin
      }

      env {
        name        = "FOUNDRY_PROJECT_ENDPOINT"
        secret_name = "foundry-project-endpoint"
      }

      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.main.client_id
      }

      env {
        name  = "FOUNDRY_MODEL"
        value = local.model_deployment_name
      }

      env {
        name  = "FOUNDRY_MODEL_MORNING"
        value = local.morning_model_deployment_name
      }

      env {
        name  = "FOUNDRY_MODEL_SPECIALIST"
        value = local.specialist_model_deployment_name
      }

      env {
        name  = "MAX_TOOL_HOPS"
        value = tostring(var.max_tool_hops)
      }

      env {
        name        = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        secret_name = "app-insights-connection-string"
      }
    }

    min_replicas = 1
    max_replicas = 5
  }

  secret {
    name                = "foundry-project-endpoint"
    key_vault_secret_id = azurerm_key_vault_secret.foundry_endpoint.id
    identity            = azurerm_user_assigned_identity.main.id
  }

  secret {
    name                = "app-insights-connection-string"
    key_vault_secret_id = azurerm_key_vault_secret.app_insights_connection_string.id
    identity            = azurerm_user_assigned_identity.main.id
  }

  ingress {
    external_enabled = false
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  lifecycle {
    ignore_changes = [
      template[0].container[0].image
    ]
  }

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_role_assignment.kv_secrets_user
  ]
}

# Mock API (internal ingress)
resource "azurerm_container_app" "mock_api" {
  count                        = var.deploy_apps ? 1 : 0
  name                         = local.mock_api_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = local.common_tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.main.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.main.id
  }

  template {
    container {
      name   = "mock-api"
      image  = "${azurerm_container_registry.main.login_server}/mock-api:latest"
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      env {
        name        = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        secret_name = "app-insights-connection-string"
      }
    }

    min_replicas = 1
    max_replicas = 3
  }

  secret {
    name                = "app-insights-connection-string"
    key_vault_secret_id = azurerm_key_vault_secret.app_insights_connection_string.id
    identity            = azurerm_user_assigned_identity.main.id
  }

  ingress {
    external_enabled = false
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  lifecycle {
    ignore_changes = [
      template[0].container[0].image
    ]
  }

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_role_assignment.kv_secrets_user
  ]
}

# Agent Provisioner Job (manual trigger)
resource "azurerm_container_app_job" "agent_provisioner" {
  count                        = var.deploy_apps ? 1 : 0
  name                         = local.agent_provisioner_job
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  tags                         = local.common_tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.main.id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.main.id
  }

  template {
    container {
      name   = "agent-provisioner"
      image  = "${azurerm_container_registry.main.login_server}/agent-provisioner:latest"
      cpu    = 0.5
      memory = "1.0Gi"

      env {
        name        = "FOUNDRY_PROJECT_ENDPOINT"
        secret_name = "foundry-project-endpoint"
      }

      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.main.client_id
      }

      env {
        name  = "FOUNDRY_MODEL"
        value = local.model_deployment_name
      }

      env {
        name  = "FOUNDRY_MODEL_MORNING"
        value = local.morning_model_deployment_name
      }

      env {
        name  = "FOUNDRY_MODEL_SPECIALIST"
        value = local.specialist_model_deployment_name
      }

      env {
        name        = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        secret_name = "app-insights-connection-string"
      }
    }
  }

  secret {
    name                = "foundry-project-endpoint"
    key_vault_secret_id = azurerm_key_vault_secret.foundry_endpoint.id
    identity            = azurerm_user_assigned_identity.main.id
  }

  secret {
    name                = "app-insights-connection-string"
    key_vault_secret_id = azurerm_key_vault_secret.app_insights_connection_string.id
    identity            = azurerm_user_assigned_identity.main.id
  }

  replica_timeout_in_seconds = 1800
  replica_retry_limit        = 1

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  lifecycle {
    ignore_changes = [
      template[0].container[0].image
    ]
  }

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_role_assignment.kv_secrets_user
  ]
}
