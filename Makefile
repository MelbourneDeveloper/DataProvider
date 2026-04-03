# agent-pmo:d75d5c8
# =============================================================================
# Standard Makefile — Nimblesite.DataProvider.Core
# Cross-platform: Linux, macOS, Windows (via GNU Make)
# All targets are language-agnostic. Add language-specific helpers below.
# =============================================================================

.PHONY: build test lint fmt fmt-check clean check ci coverage coverage-check setup

# -----------------------------------------------------------------------------
# OS Detection — portable commands for Linux, macOS, and Windows
# On Windows, run via GNU Make with PowerShell (e.g., make from Git Bash or
# choco install make). The $(OS) variable is set to "Windows_NT" automatically.
# -----------------------------------------------------------------------------
ifeq ($(OS),Windows_NT)
  SHELL := powershell.exe
  .SHELLFLAGS := -NoProfile -Command
  RM = Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  MKDIR = New-Item -ItemType Directory -Force
  HOME ?= $(USERPROFILE)
else
  RM = rm -rf
  MKDIR = mkdir -p
endif

# Coverage threshold (override in CI via env var or per-repo)
COVERAGE_THRESHOLD ?= 90

# =============================================================================
# PRIMARY TARGETS (uniform interface — do not rename)
# =============================================================================

## build: Compile/assemble all artifacts
build:
	@echo "==> Building..."
	$(MAKE) _build

## test: Run full test suite with coverage
test:
	@echo "==> Testing..."
	$(MAKE) _test

## lint: Run all linters (fails on any warning)
lint:
	@echo "==> Linting..."
	$(MAKE) _lint

## fmt: Format all code in-place
fmt:
	@echo "==> Formatting..."
	$(MAKE) _fmt

## fmt-check: Check formatting without modifying
fmt-check:
	@echo "==> Checking format..."
	$(MAKE) _fmt_check

## clean: Remove all build artifacts
clean:
	@echo "==> Cleaning..."
	$(MAKE) _clean

## check: lint + test (pre-commit)
check: lint test

## ci: lint + test + build (full CI simulation)
ci: lint test build

## coverage: Generate coverage report
coverage:
	@echo "==> Coverage report..."
	$(MAKE) _coverage

## coverage-check: Assert thresholds (exits non-zero if below)
coverage-check:
	@echo "==> Checking coverage thresholds..."
	$(MAKE) _coverage_check

## setup: Post-create dev environment setup (used by devcontainer)
setup:
	@echo "==> Setting up development environment..."
	$(MAKE) _setup
	@echo "==> Setup complete. Run 'make ci' to validate."

# =============================================================================
# LANGUAGE-SPECIFIC IMPLEMENTATIONS
# Nimblesite.DataProvider.Core is a multi-language repo: C#/.NET (primary), Rust, TypeScript
# =============================================================================

_build: _build_dotnet _build_rust _build_ts

_test: _test_dotnet _test_rust

_lint: _lint_dotnet _lint_rust _lint_ts

_fmt: _fmt_dotnet _fmt_rust

_fmt_check: _fmt_check_dotnet _fmt_check_rust

_clean: _clean_dotnet _clean_rust _clean_ts

_coverage: _coverage_dotnet

_coverage_check: _coverage_check_dotnet

_setup: _setup_dotnet _setup_ts

# --- C#/.NET ---
_build_dotnet:
	dotnet build DataProvider.sln --configuration Release

_test_dotnet:
	dotnet test DataProvider.sln --configuration Release \
	  --settings coverlet.runsettings \
	  --collect:"XPlat Code Coverage" \
	  --results-directory TestResults \
	  --verbosity normal

_lint_dotnet:
	dotnet build DataProvider.sln --configuration Release
	dotnet csharpier check .

_fmt_dotnet:
	dotnet csharpier format .

_fmt_check_dotnet:
	dotnet csharpier check .

_clean_dotnet:
ifeq ($(OS),Windows_NT)
	Get-ChildItem -Recurse -Directory -Include bin,obj -Exclude lql-lsp-rust | Remove-Item -Recurse -Force
	$(RM) TestResults
else
	find . -type d \( -name bin -o -name obj \) -not -path './Lql/lql-lsp-rust/*' | xargs rm -rf
	$(RM) TestResults
endif

_coverage_dotnet:
	dotnet test DataProvider.sln --configuration Release \
	  --settings coverlet.runsettings \
	  --collect:"XPlat Code Coverage" \
	  --results-directory TestResults \
	  --verbosity normal
	reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" \
	  -targetdir:coverage/html -reporttypes:Html
ifeq ($(OS),Windows_NT)
	Start-Process coverage/html/index.html
else ifeq ($(shell uname -s),Darwin)
	open coverage/html/index.html
else
	xdg-open coverage/html/index.html
endif

_coverage_check_dotnet:
	@COVERAGE=$$(dotnet test DataProvider.sln --configuration Release \
	  --settings coverlet.runsettings \
	  --collect:"XPlat Code Coverage" \
	  --results-directory TestResults \
	  --verbosity quiet 2>/dev/null | grep -oP 'Line coverage: \K[0-9.]+' | tail -1); \
	THRESHOLD=$${COVERAGE_THRESHOLD:-80}; \
	if [ -z "$$COVERAGE" ]; then \
	  echo "WARNING: Could not extract coverage percentage"; \
	else \
	  echo "Coverage: $$COVERAGE% (threshold: $$THRESHOLD%)"; \
	  if [ $$(echo "$$COVERAGE < $$THRESHOLD" | bc -l) -eq 1 ]; then \
	    echo "FAIL: Coverage $$COVERAGE% is below threshold $$THRESHOLD%"; \
	    exit 1; \
	  fi; \
	fi

_setup_dotnet:
	dotnet restore
	dotnet tool restore

# --- RUST (LQL LSP) ---
_build_rust:
	cd Lql/lql-lsp-rust && cargo build --release

_test_rust:
	cd Lql/lql-lsp-rust && cargo test --workspace

_lint_rust:
	cd Lql/lql-lsp-rust && cargo fmt --all --check
	cd Lql/lql-lsp-rust && cargo clippy --workspace --all-targets -- -D warnings

_fmt_rust:
	cd Lql/lql-lsp-rust && cargo fmt --all

_fmt_check_rust:
	cd Lql/lql-lsp-rust && cargo fmt --all --check

_clean_rust:
	cd Lql/lql-lsp-rust && cargo clean

# --- TYPESCRIPT (LQL Extension) ---
_build_ts:
	cd Lql/LqlExtension && npm install --no-audit --no-fund && npm run compile

_lint_ts:
	cd Lql/LqlExtension && npm run lint

_clean_ts:
	$(RM) Lql/LqlExtension/node_modules Lql/LqlExtension/out

_setup_ts:
	cd Lql/LqlExtension && npm install --no-audit --no-fund

# =============================================================================
# HELP
# =============================================================================
help:
	@echo "Available targets:"
	@echo "  build          - Compile/assemble all artifacts"
	@echo "  test           - Run full test suite with coverage"
	@echo "  lint           - Run all linters (errors mode)"
	@echo "  fmt            - Format all code in-place"
	@echo "  fmt-check      - Check formatting (no modification)"
	@echo "  clean          - Remove build artifacts"
	@echo "  check          - lint + test (pre-commit)"
	@echo "  ci             - lint + test + build (full CI)"
	@echo "  coverage       - Generate and open coverage report"
	@echo "  coverage-check - Assert coverage thresholds"
	@echo "  setup          - Post-create dev environment setup"
