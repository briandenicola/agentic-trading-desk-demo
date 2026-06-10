resource "azurerm_key_vault" "main" {
  name                       = local.key_vault_name
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  purge_protection_enabled   = false
  rbac_authorization_enabled = true
  tags                       = local.common_tags

  # Secure posture: public network access is disabled. Container Apps resolve
  # secret references via the managed identity over the Azure trusted-services
  # path, so runtime is unaffected. NOTE: this also means `terraform apply` for
  # KV secrets must run from inside the VNet (or with the vault temporarily
  # opened); routine infra changes are applied via `az` against the running apps.
  public_network_access_enabled = false
}

# Grant current deployment identity access to create secrets
resource "azurerm_role_assignment" "kv_admin" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}
