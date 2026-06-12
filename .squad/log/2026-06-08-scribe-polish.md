# 2026-06-08 — Scribe/QA Phase 8 polish

## Summary
- T043: Rewrote README and Copilot instructions for C#/.NET 10 + Microsoft Agent Framework + Azure AI Foundry + Azure Container Apps; expanded `.env.example`; retired `requirements.txt` as a comment-only compatibility marker.
- T044: Wired deployed orchestration-api CORS to the derived ui-app HTTPS origin (`local.ui_app_origin`); updated ui-app Dockerfile to run as `USER $APP_UID`; verified .NET service Dockerfiles already run non-root.
- T046: Marked `007-foundry-migration` and `003-container-deployment` backlog cards delivered/realized by `001-morning-planning-outreach`; appended the decision to `.squad\decisions.md`.

## Verification notes
- Run after edits: Terraform fmt/validate, optional gitleaks, and `dotnet build AgenticTradersDesk.sln --nologo -v q`.

## Verification results
- `terraform -chdir=infra fmt`; `terraform -chdir=infra fmt -check`; `terraform -chdir=infra validate` — passed.
- `gitleaks detect --source . --no-banner` — skipped; `gitleaks` is not installed on PATH.
- `dotnet build AgenticTradersDesk.sln --nologo -v q` — passed (0 warnings, 0 errors).
