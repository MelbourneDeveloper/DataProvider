---
name: submit-pr
description: Submit a pull request following DataProvider project standards
---

# Submit Pull Request

Create a pull request following project requirements.

## Get Context

Get the diff between main and current branch:

```bash
git diff main...HEAD
```

DO NOT include commit messages or branch names in analysis.

Read the PR template:

```bash
cat .github/PULL_REQUEST_TEMPLATE.md
```

## Write PR Description

The template has three sections (gh will auto-populate structure):

### TLDR
- Few lines maximum
- Bullet points if many changes
- For people who won't read details

### Brief Details
- Keep BRIEF
- May reference code/files
- What changed and why

### How Do The Tests Prove This Works? (CRITICAL)
- Point to specific test files/methods
- Explain WHAT each test verifies
- Show HOW tests prove correctness, not just "tests added"

## Requirements

- TIGHT - no fluff
- ACCURATE - based on actual diff

## Submit

```bash
gh pr create
```
