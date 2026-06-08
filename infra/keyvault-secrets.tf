# Foundry project endpoint for runtime consumption
resource "azurerm_key_vault_secret" "foundry_endpoint" {
  name         = "FOUNDRY-PROJECT-ENDPOINT"
  value        = local.foundry_project_endpoint
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.kv_admin]
}

# Application Insights connection string
resource "azurerm_key_vault_secret" "app_insights_connection_string" {
  name         = "APPLICATIONINSIGHTS-CONNECTION-STRING"
  value        = azurerm_application_insights.main.connection_string
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.kv_admin]
}

# Application Insights instrumentation key (legacy, but some SDKs still use it)
resource "azurerm_key_vault_secret" "app_insights_key" {
  name         = "APPINSIGHTS-INSTRUMENTATIONKEY"
  value        = azurerm_application_insights.main.instrumentation_key
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_role_assignment.kv_admin]
}
