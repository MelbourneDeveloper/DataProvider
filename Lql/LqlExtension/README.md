# LQL VS Code Extension

A VS Code extension providing full language support for Lambda Query Language (LQL) -- powered by a Rust LSP server with ANTLR-generated parser, real database schema IntelliSense, and optional AI-powered completions.

```
VS Code Extension (TypeScript)
    |
    └── stdio JSON-RPC --> lql-lsp (Rust binary)
                                |
                                ├── lql-parser (ANTLR4 grammar -> Rust)
                                ├── lql-analyzer (completions, hover, diagnostics, schema)
                                └── tokio-postgres (live database schema introspection)
```

## Features

- Schema-aware completions (tables, columns from live database)
- Dot-triggered column completions with type info
- Hover documentation for operations, functions, tables, columns
- Real-time diagnostics from ANTLR parse
- Document symbols for `let` bindings
- Optional AI completions via Ollama

## Building

```bash
# Rust LSP
cd Lql/lql-lsp-rust && cargo build --release

# VS Code extension
cd Lql/LqlExtension && npm install && npx tsc --project tsconfig.json
```

## Documentation

- Parent README: [Lql/README.md](../README.md)
- LQL spec: [docs/specs/lql-spec.md](../../docs/specs/lql-spec.md)
- LQL reference: [lql-lsp-rust/crates/lql-reference.md](../lql-lsp-rust/crates/lql-reference.md)
