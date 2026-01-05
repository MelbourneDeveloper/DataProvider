# Contributing to DataProvider

Thanks for your interest in contributing. Please read this guide carefully before submitting a PR.

## What We Accept

### Always Welcome (No Issue Required)
- **Bug fixes** with tests that reproduce the issue
- **New tests** that cover untested code paths (must not duplicate existing tests)
- **Typo fixes** and factual corrections in documentation
- **Build/CI fixes** that solve real problems

### Requires Discussion First
- **New features** - Open a Discussion first, then create an Issue if approved
- **Refactoring** - Must have a clear rationale and maintainer approval
- **Architecture changes** - Require an Issue with detailed proposal
- **Documentation additions** - Beyond typo fixes, discuss first

## Before You Submit

1. **Check existing Issues and Discussions** - Your idea may already be tracked
2. **Run the tests** - `dotnet test` must pass
3. **Format your code** - `dotnet csharpier .` from the root folder
4. **Keep changes minimal** - Only change what's necessary for your fix/feature

## PR Requirements

- PRs must solve a specific, identifiable problem
- Include tests for bug fixes
- Don't introduce unrelated changes
- Don't add features without prior approval via Issue

## What Will Be Rejected

- PRs with no clear purpose or rationale
- Bulk formatting/style changes
- "Improvements" nobody asked for
- Features without an approved Issue
- Duplicate tests or tests that don't add coverage
- AI-generated spam PRs with generic changes

## Code Standards

See [CLAUDE.md](CLAUDE.md) for coding conventions. Key points:
- No exceptions - use `Result<T,E>`
- No classes - use records with static methods
- No interfaces - use `Func<T>`/`Action<T>`
- All tests must be integration tests (no mocks)
- Files under 450 LOC, functions under 20 LOC

## Process

1. **Bug fix**: Fork, fix, test, PR
2. **Feature**: Discussion -> Issue -> Fork -> Implement -> PR

Questions? Open a Discussion.
