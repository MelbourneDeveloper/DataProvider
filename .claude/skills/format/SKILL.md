---
name: format
description: Format C# code with CSharpier. Use when asked to format code, fix formatting, or before committing changes.
disable-model-invocation: true
allowed-tools: Bash(dotnet csharpier *)
---

# Format

Run CSharpier to format all C# code in the repository.

```bash
dotnet csharpier /Users/christianfindlay/Documents/Code/DataProvider
```

If formatting a specific directory:

```bash
dotnet csharpier /Users/christianfindlay/Documents/Code/DataProvider/$ARGUMENTS
```
