# Detect the public egress IP of whoever runs `terraform apply` (handy if we ever
# tighten network_acls to a deny-list; see note on the vault below).
# Only needed when the vault is provisioned (FULL/LIVE mode).
data "http" "deployer_ip" {
  count = local.use_key_vault ? 1 : 0
  url   = "https://api.ipify.org"
}

# Key Vault is provisioned ONLY in FULL/LIVE mode (enable_foundry = true). In pure
# DEMO mode it is skipped entirely — see locals.use_key_vault — because the only
# genuinely runtime-required secret is the Foundry endpoint, and DEMO carries the
# remaining config as direct Container App secrets.
resource "azurerm_key_vault" "main" {
  count                      = local.use_key_vault ? 1 : 0
  name                       = local.key_vault_name
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  purge_protection_enabled   = false
  rbac_authorization_enabled = true
  tags                       = local.common_tags

  # Network posture: the public endpoint stays ENABLED and access is gated by
  # Azure RBAC (rbac_authorization_enabled = true), not a network firewall.
  #
  # A default-deny firewall is NOT viable in this architecture: Container Apps
  # resolve Key Vault secret *references* over a path that is NOT covered by the
  # AzureServices bypass, and there is no private endpoint here — so a deny rule
  # blocks the apps from starting (secret resolution fails at create time). The
  # earlier `public_network_access_enabled = false` also broke fresh-region
  # provisioning because the deployer could not write the secrets below.
  #
  # Proper hardening (private endpoint + VNet-integrated Container Apps) is
  # tracked separately; until then, RBAC is the gate and the network is open.
  public_network_access_enabled = true

  network_acls {
    default_action = "Allow"
    bypass         = "AzureServices"
  }
}

# Grant current deployment identity access to create secrets
resource "azurerm_role_assignment" "kv_admin" {
  count                = local.use_key_vault ? 1 : 0
  scope                = azurerm_key_vault.main[0].id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}
