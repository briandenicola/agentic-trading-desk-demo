# Azure Container Apps Deployment

## Status: Delivered / realized by `001-morning-planning-outreach`

## Priority: P1 (Critical Path)

## Description
Containerize the application and deploy to Azure Container Apps with
infrastructure-as-code (Terraform or Bicep).

`specs\001-morning-planning-outreach\` realizes this backlog card for Demo 1: Terraform in `infra\` provisions Container Apps, ACR, Key Vault, managed identity, App Insights, and optional Foundry resources; Dockerfiles under `src\*\Dockerfile` build the UI, orchestration API, mock API, and agent provisioner; workflows under `.github\workflows\` handle CI/CD.

## Scope
- [x] Multi-stage `Dockerfile` files for `src\ui-app`, `src\orchestration-api`, `src\mock-api`, and `src\agent-provisioner` with non-root runtime users.
- [x] Azure Container Apps environment with managed identity.
- [x] Key Vault for Foundry/App Insights secret references.
- [x] GitHub Actions CD pipeline (build → push to ACR → deploy).
- [x] Health probes on `/healthz` and readiness probes on `/readyz` for APIs.
- [x] Container Apps scaling configured in `infra\containerapps.tf`.

## Acceptance Criteria
- [x] Container builds are defined through `tasks\Taskfile.build.yml` and the service Dockerfiles.
- [x] `infra\` contains IaC for Container Apps + Key Vault + ACR.
- [x] CD pipeline triggers on merge to `main`.
- [x] Containers run as non-root (`USER $APP_UID` in runtime images).
- [x] Deployment secrets are injected from Key Vault secret references where secrets are required.
- [x] App responds through the public HTTPS `ui-app` Container App endpoint (`ui_app_url` output).

## Dependencies
- `001-morning-planning-outreach` (delivered the deployable Demo 1 slice)

## Notes
Use `task local:up` / `task local:down` for local Docker Compose and `task cloud:up`, `task build:all`, `task cloud:deploy` for Azure Container Apps.
