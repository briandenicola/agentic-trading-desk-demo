# ADR 0001: Adopt Spec Kit + Squad Framework

## Status
ACCEPTED

## Context
The Client CV demo is transitioning from a prototype to a production-ready application.
We need a structured delivery process that works well with AI-assisted development,
provides traceability from requirements to code, and supports team coordination.

## Decision
Adopt the Spec Kit + Squad framework from `briandenicola/template`:
- **Spec Kit** (`.specify/`) for spec-driven delivery pipeline
- **Squad** (`.squad/`) for AI team coordination
- Constitution (`.specify/memory/constitution.md`) as the binding governance document

## Consequences
- All new features must follow the specify → clarify → plan → tasks → implement pipeline.
- AI agents must read the constitution before editing code.
- Decision log (`.squad/decisions.md`) becomes the canonical record of project choices.
- Constitution amendments require ADR + PR (§22).
