#!/usr/bin/env bash
# ============================================================================
# Build the Rust LSP server (release) and package the VS Code extension (.vsix)
#
# Usage:
#   ./build-vsix.sh            # Build + package
#   ./build-vsix.sh --install  # Build + package + install into VS Code
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
EXT_DIR="$SCRIPT_DIR/../LqlExtension"
LSP_BINARY="$SCRIPT_DIR/target/release/lql-lsp"

# ── 1. Build Rust LSP (release) ─────────────────────────────────────────────
echo ">>> Building LQL LSP server (release)..."
cd "$SCRIPT_DIR"
cargo build --release -p lql-lsp 2>&1
echo ">>> LSP binary: $LSP_BINARY"

# ── 2. Copy binary into extension ───────────────────────────────────────────
echo ">>> Copying LSP binary into extension bin/"
mkdir -p "$EXT_DIR/bin"
cp "$LSP_BINARY" "$EXT_DIR/bin/lql-lsp"
chmod +x "$EXT_DIR/bin/lql-lsp"

# ── 3. Build the VS Code extension ──────────────────────────────────────────
echo ">>> Installing extension npm dependencies..."
cd "$EXT_DIR"
npm install --no-audit --no-fund

echo ">>> Compiling TypeScript..."
npm run compile

echo ">>> Packaging VSIX..."
npx vsce package --no-git-tag-version --no-update-package-json
VSIX_FILE=$(ls -t "$EXT_DIR"/*.vsix 2>/dev/null | head -1)

if [ -z "$VSIX_FILE" ]; then
    echo "ERROR: VSIX file not found after packaging"
    exit 1
fi

echo ">>> VSIX: $VSIX_FILE"

# ── 4. Optionally install into VS Code ──────────────────────────────────────
if [ "${1:-}" = "--install" ]; then
    echo ">>> Installing extension into VS Code..."
    code --install-extension "$VSIX_FILE" --force
    echo ">>> Extension installed. Reload VS Code to activate."
fi

echo ""
echo "=== Build complete ==="
echo "  LSP binary: $LSP_BINARY"
echo "  VSIX:       $VSIX_FILE"
