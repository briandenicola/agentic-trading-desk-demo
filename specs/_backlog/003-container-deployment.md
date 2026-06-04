# Azure Container Apps Deployment

## Priority: P1 (Critical Path)

## Description
Containerize the application and deploy to Azure Container Apps with
infrastructure-as-code (Terraform or Bicep).

## Scope
- Multi-stage `Dockerfile` (build + runtime, non-root).
- Azure Container Apps environment with managed identity.
- Key Vault for secrets (Azure OpenAI key, connector credentials).
- GitHub Actions CD pipeline (build → push to ACR → deploy).
- Health probe on `/healthz`.
- Auto-scaling rules (HTTP concurrent requests).

## Acceptance Criteria
- [ ] `docker build` produces a working image that serves the app.
- [ ] `infrastructure/` contains IaC for Container Apps + Key Vault + ACR.
- [ ] CD pipeline triggers on merge to `main`.
- [ ] Container runs as non-root with read-only filesystem.
- [ ] Secrets injected from Key Vault, not env vars in the container spec.
- [ ] App responds on a public HTTPS endpoint.

## Dependencies
- 002-authentication (should deploy with auth enabled)

## Notes
Use the existing `Taskfile.yaml` pattern for `task up` / `task down`.
Consider Dapr for service-to-service calls if adding more microservices later.
