# Detect the public egress IP of whoever runs `terraform apply` so the Key Vault
# firewall can allowlist them long enough to write the secrets below. This is what
# makes a fresh-region bring-up work; re-applies refresh the IP automatically.
data "http" "deployer_ip" {
  url = "https://api.ipify.org"
}

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

  # Secure posture: a DEFAULT-DENY firewall, not a fully-private vault.
  # public_network_access_enabled MUST stay true for the network_acls to be
  # honored — with it set to false Azure ignores the ACLs and blocks every
  # caller (including the deployer), which broke fresh-region `terraform apply`
  # of the KV secrets (ForbiddenByConnection). bypass = AzureServices keeps the
  # Container Apps managed-identity secret-reference path working at runtime, and
  # the deployer's current public IP is allowlisted so the bootstrap apply can
  # write the secrets. Everything else is denied by default.
  public_network_access_enabled = true

  network_acls {
    default_action = "Deny"
    bypass         = "AzureServices"
    ip_rules       = [chomp(data.http.deployer_ip.response_body)]
  }
}

# Grant current deployment identity access to create secrets
resource "azurerm_role_assignment" "kv_admin" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}
