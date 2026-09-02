# These secrets exist only when Key Vault is provisioned (FULL/LIVE mode). In pure
# DEMO mode (local.use_key_vault = false) the container apps carry the equivalent
# values as direct Container App secrets instead — see containerapps.tf.

# Foundry project endpoint for runtime consumption
resource "azurerm_key_vault_secret" "foundry_endpoint" {
  count        = local.use_key_vault ? 1 : 0
  name         = "FOUNDRY-PROJECT-ENDPOINT"
  value        = local.foundry_project_endpoint
  key_vault_id = azurerm_key_vault.main[0].id
  depends_on   = [azurerm_role_assignment.kv_admin]
}

# Application Insights connection string
resource "azurerm_key_vault_secret" "app_insights_connection_string" {
  count        = local.use_key_vault ? 1 : 0
  name         = "APPLICATIONINSIGHTS-CONNECTION-STRING"
  value        = azurerm_application_insights.main.connection_string
  key_vault_id = azurerm_key_vault.main[0].id
  depends_on   = [azurerm_role_assignment.kv_admin]
}

# Application Insights instrumentation key (legacy, but some SDKs still use it)
resource "azurerm_key_vault_secret" "app_insights_key" {
  count        = local.use_key_vault ? 1 : 0
  name         = "APPINSIGHTS-INSTRUMENTATIONKEY"
  value        = azurerm_application_insights.main.instrumentation_key
  key_vault_id = azurerm_key_vault.main[0].id
  depends_on   = [azurerm_role_assignment.kv_admin]
}
