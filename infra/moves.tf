# State refactor: the Key Vault and its dependent resources gained a `count`
# (gated on local.use_key_vault) so they can be skipped entirely in pure DEMO
# mode. These `moved` blocks migrate any existing (un-counted) state from
# `<resource>` to `<resource>[0]` so a FULL/LIVE apply updates the vault
# in-place instead of destroying and recreating it. In DEMO mode (count = 0)
# the destination does not exist and the old state is destroyed, which is the
# intended behavior.

moved {
  from = azurerm_key_vault.main
  to   = azurerm_key_vault.main[0]
}

moved {
  from = azurerm_role_assignment.kv_admin
  to   = azurerm_role_assignment.kv_admin[0]
}

moved {
  from = azurerm_role_assignment.kv_secrets_user
  to   = azurerm_role_assignment.kv_secrets_user[0]
}

moved {
  from = azurerm_key_vault_secret.foundry_endpoint
  to   = azurerm_key_vault_secret.foundry_endpoint[0]
}

moved {
  from = azurerm_key_vault_secret.app_insights_connection_string
  to   = azurerm_key_vault_secret.app_insights_connection_string[0]
}

moved {
  from = azurerm_key_vault_secret.app_insights_key
  to   = azurerm_key_vault_secret.app_insights_key[0]
}
