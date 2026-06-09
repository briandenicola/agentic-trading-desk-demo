# Evaluation & Prompt Testing Framework

## Priority: P3 (Medium)

## Status: Selected for NEXT iteration — order 6 of 6 (per 2026-06-09 direction). Rescoped from pytest to the C#/.NET stack. NOTE: depends on 004-test-suite, which is NOT in the current iteration set — evaluate building the harness standalone or pulling 004 forward.

## Description
Build an evaluation harness that tests agent outputs against golden
examples, measuring quality regressions when prompts or tools change.

## Scope
- Golden test cases per scene (input payload → expected output structure).
- Evaluation metrics: schema compliance, key-field presence, ranking accuracy.
- .NET-based eval runner (xUnit, separate slow suite from unit tests; uses the LLM/Foundry).
- CI integration: eval runs on prompt changes, reports quality delta.
- Prompt versioning: git-tracked prompts with changelog.

## Acceptance Criteria
- [ ] At least 3 golden test cases per scene.
- [ ] Eval runner reports pass/fail with detailed diff.
- [ ] CI triggers eval on changes to `src\orchestration-api\Prompts\`.
- [ ] Quality regression blocks PR merge (configurable threshold).
- [ ] Results stored for trend analysis.

## Dependencies
- 004-test-suite (testing infrastructure)

## Notes
Consider Azure AI Foundry Evaluations or `promptfoo` as evaluation backends.
Start with simple schema + key-field checks before adding LLM-as-judge.
