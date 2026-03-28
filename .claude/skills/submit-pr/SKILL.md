---
name: submit-pr
description: Creates a pull request with a well-structured description after verifying CI passes. Use when the user asks to submit, create, or open a pull request.
disable-model-invocation: true
---

# Submit PR

Create a pull request for the current branch with a well-structured description.

## Steps

1. Run `make ci` — must pass completely before creating PR
2. Determine the PR title from recent commits and changed files
3. Read the PR template from `.github/PULL_REQUEST_TEMPLATE.md`
4. Fill in:
   - TLDR: one sentence
   - What Was Added: new files, features, deps
   - What Was Changed/Deleted: modified behaviour
   - How Tests Prove It Works: specific test names or output
   - Spec/Doc Changes: if any
   - Breaking Changes: yes/no + description
5. Use `gh pr create` with the filled template

## Rules

- Never create a PR if `make ci` fails
- PR description must be specific ��� no vague placeholders
- Link to the relevant GitHub issue if one exists

## Success criteria

- `make ci` passed
- PR created with `gh pr create`
- PR URL returned to user
