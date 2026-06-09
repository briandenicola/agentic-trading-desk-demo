output "resource_group_name" {
  description = "Resource group name"
  value       = azurerm_resource_group.main.name
}

output "acr_login_server" {
  description = "Azure Container Registry login server"
  value       = azurerm_container_registry.main.login_server
}

output "acr_name" {
  description = "Azure Container Registry name"
  value       = azurerm_container_registry.main.name
}

output "ui_app_fqdn" {
  description = "UI App public FQDN"
  value       = try(azurerm_container_app.ui_app[0].ingress[0].fqdn, "")
}

output "ui_app_url" {
  description = "UI App public URL"
  value       = try("https://${azurerm_container_app.ui_app[0].ingress[0].fqdn}", "")
}

output "orchestration_api_fqdn" {
  description = "Orchestration API internal FQDN"
  value       = try(azurerm_container_app.orchestration_api[0].ingress[0].fqdn, "")
}

output "mock_api_fqdn" {
  description = "Mock API internal FQDN"
  value       = try(azurerm_container_app.mock_api[0].ingress[0].fqdn, "")
}

output "foundry_project_endpoint" {
  description = "Azure AI Foundry project endpoint"
  value       = local.foundry_project_endpoint
}

output "foundry_model_deployment" {
  description = "Foundry model deployment name"
  value       = local.model_deployment_name
}

output "app_insights_connection_string" {
  description = "Application Insights connection string"
  value       = azurerm_application_insights.main.connection_string
  sensitive   = true
}
