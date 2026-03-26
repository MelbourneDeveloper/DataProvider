---
name: fmt
description: Formats all code in this repo (C# with CSharpier, Rust with cargo fmt). Use when the user asks to format code, fix formatting, or before committing changes.
---

# Format

Format all code in this repo.

## Steps

1. Run `make fmt`
2. Run `make fmt-check` to confirm clean
3. Report which files were modified

## Success criteria

- `make fmt-check` exits with code 0 after formatting
