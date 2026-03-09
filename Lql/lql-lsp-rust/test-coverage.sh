#!/usr/bin/env bash
# ============================================================================
# Run all LSP Rust tests and generate coverage report
#
# Usage:
#   ./test-coverage.sh           # Run tests + coverage summary
#   ./test-coverage.sh --html    # Also generate HTML report
#   ./test-coverage.sh --xml     # Also generate Cobertura XML (for CI)
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

TARPAULIN_ARGS=(
    --workspace
    --skip-clean
    --timeout 120
    --engine llvm
    --out stdout
)

# ── Parse flags ──────────────────────────────────────────────────────────────
OPEN_HTML=false
for arg in "$@"; do
    case "$arg" in
        --html)
            TARPAULIN_ARGS+=(--out html)
            OPEN_HTML=true
            ;;
        --xml)
            TARPAULIN_ARGS+=(--out xml)
            ;;
    esac
done

# ── 1. Run tests (plain cargo test for quick feedback) ───────────────────────
echo ">>> Running cargo test..."
cargo test --workspace 2>&1
echo ""

# ── 2. Run coverage with tarpaulin ──────────────────────────────────────────
echo ">>> Running cargo-tarpaulin for coverage..."
cargo tarpaulin "${TARPAULIN_ARGS[@]}" 2>&1

# ── 3. Open HTML report if requested ────────────────────────────────────────
if [ "$OPEN_HTML" = true ] && [ -f "$SCRIPT_DIR/tarpaulin-report.html" ]; then
    echo ">>> Opening HTML coverage report..."
    open "$SCRIPT_DIR/tarpaulin-report.html" 2>/dev/null || true
fi

echo ""
echo "=== Done ==="
