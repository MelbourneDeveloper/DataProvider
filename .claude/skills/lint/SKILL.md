---
name: lint
description: Runs all linters and format checks across C#, Rust, and TypeScript, then fixes any issues found. Use when the user asks to lint, check code quality, or fix linting errors.
---

# Lint

Run all linters and report issues.

## Steps

1. Run `make lint`
2. Run `make fmt-check`
3. Report all issues found (file, line, rule, message)
4. If issues found, fix them and re-run to confirm clean

## Rules

- Never suppress a lint warning with an ignore comment
- Fix the code to satisfy the linter
- If a rule seems wrong for a specific case, document why in code comments

## Success criteria

- `make lint` exits with code 0
- `make fmt-check` exits with code 0
- Zero warnings or errors output
