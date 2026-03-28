---
name: ci-prep
description: Prepares the current branch for CI by running lint, test, coverage, and build in sequence, fixing issues at each step. Use before pushing a branch or when the user wants to verify the branch will pass CI.
---

# CI Prep

Prepare the current state for CI. Ensures the branch will pass CI before pushing.

## Steps

1. Read and analyze .github/workflows/ci.yml
2. Generate a TODO list of the items you need to run in order to reproduce the ci workflow
3. Work through the TODO list
4. Fix issues as you see them.
5. Repeat until the ci passes

## Rules

- Do not push if any step fails
- Fix issues found in each step before moving to the next
- Never skip steps or suppress errors
- Fix bugs; do not modify tests

## Success criteria

- `make ci` exits with code 0
- Coverage threshold met
- Build artifacts produced successfully
