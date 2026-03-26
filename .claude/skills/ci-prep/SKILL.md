---
name: ci-prep
description: Prepares the current branch for CI by running lint, test, coverage, and build in sequence, fixing issues at each step. Use before pushing a branch or when the user wants to verify the branch will pass CI.
---

# CI Prep

Prepare the current state for CI. Ensures the branch will pass CI before pushing.

## Steps

1. Run `make lint` — fix any issues before continuing
2. Run `make test` — fix any failures before continuing
3. Run `make coverage-check` — coverage must meet threshold
4. Run `make build` — confirm build succeeds
5. Report: all clear / what failed

## Rules

- Do not push if any step fails
- Fix issues found in each step before moving to the next
- Never skip steps or suppress errors

## Success criteria

- `make ci` exits with code 0
- Coverage threshold met
- Build artifacts produced successfully
