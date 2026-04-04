# agent-pmo:d75d5c8
# =============================================================================
# Standard Makefile — Nimblesite.DataProvider.Core
# Cross-platform: Linux, macOS, Windows (via GNU Make)
# All targets are language-agnostic. Add language-specific helpers below.
# =============================================================================

.PHONY: build test lint fmt fmt-check clean check ci coverage setup

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

# All .NET test projects (one per line for readability)
DOTNET_TEST_PROJECTS = \
  DataProvider/Nimblesite.DataProvider.Tests \
  DataProvider/Nimblesite.DataProvider.Example.Tests \
  Lql/Nimblesite.Lql.Tests \
  Lql/Nimblesite.Lql.Cli.SQLite.Tests \
  Lql/Nimblesite.Lql.TypeProvider.FSharp.Tests \
  Migration/Nimblesite.DataProvider.Migration.Tests \
  Sync/Nimblesite.Sync.Tests \
  Sync/Nimblesite.Sync.SQLite.Tests \
  Sync/Nimblesite.Sync.Postgres.Tests \
  Sync/Nimblesite.Sync.Integration.Tests \
  Sync/Nimblesite.Sync.Http.Tests

# =============================================================================
# PRIMARY TARGETS (uniform interface — do not rename)
# =============================================================================

## build: Compile/assemble all artifacts
build:
	@echo "==> Building..."
	$(MAKE) _build

## test: Run full test suite with coverage enforcement
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

## coverage: Generate HTML coverage report (runs tests first)
coverage:
	@echo "==> Coverage report..."
	$(MAKE) _coverage

## setup: Post-create dev environment setup (used by devcontainer)
setup:
	@echo "==> Setting up development environment..."
	$(MAKE) _setup
	@echo "==> Setup complete. Run 'make ci' to validate."

# =============================================================================
# LANGUAGE-SPECIFIC IMPLEMENTATIONS
# =============================================================================

_build: _build_dotnet _build_rust _build_ts

_test: _test_dotnet _test_rust _test_ts

_lint: _lint_dotnet _lint_rust _lint_ts

_fmt: _fmt_dotnet _fmt_rust

_fmt_check: _fmt_check_dotnet _fmt_check_rust

_clean: _clean_dotnet _clean_rust _clean_ts

_coverage: _coverage_dotnet

_setup: _setup_dotnet _setup_ts

# =============================================================================
# COVERAGE ENFORCEMENT (shared shell logic)
# =============================================================================
# Each test target collects coverage, compares against coverage-thresholds.json thresholds,
# fails hard if below, and ratchets up coverage-thresholds.json if above.
#
# coverage-thresholds.json format:
#   { "default_threshold": 90, "projects": { "Path/To/Project": 90, ... } }
# =============================================================================

# --- C#/.NET ---
_build_dotnet:
	dotnet build DataProvider.sln --configuration Release

_test_dotnet:
	@FAIL=0; \
	for proj in $(DOTNET_TEST_PROJECTS); do \
	  echo ""; \
	  echo "============================================================"; \
	  THRESHOLD=$$(jq -r ".projects[\"$$proj\"] // .default_threshold" coverage-thresholds.json); \
	  echo "==> Testing $$proj (threshold: $$THRESHOLD%)"; \
	  echo "============================================================"; \
	  rm -rf "$$proj/TestResults"; \
	  dotnet test "$$proj" --configuration Release \
	    --settings coverlet.runsettings \
	    --collect:"XPlat Code Coverage" \
	    --results-directory "$$proj/TestResults" \
	    --verbosity normal; \
	  if [ $$? -ne 0 ]; then \
	    echo "FAIL: Tests failed for $$proj"; \
	    exit 1; \
	  fi; \
	  COBERTURA=$$(find "$$proj/TestResults" -name "coverage.cobertura.xml" -type f 2>/dev/null | head -1); \
	  if [ -z "$$COBERTURA" ]; then \
	    echo "FAIL: No coverage file produced for $$proj"; \
	    exit 1; \
	  fi; \
	  LINE_RATE=$$(sed -n 's/.*line-rate="\([0-9.]*\)".*/\1/p' "$$COBERTURA" | head -1); \
	  if [ -z "$$LINE_RATE" ]; then \
	    echo "FAIL: Could not parse line-rate from $$COBERTURA"; \
	    exit 1; \
	  fi; \
	  COVERAGE=$$(echo "$$LINE_RATE * 100" | bc -l); \
	  COVERAGE_FMT=$$(printf "%.2f" $$COVERAGE); \
	  echo ""; \
	  echo "  Coverage: $$COVERAGE_FMT% | Threshold: $$THRESHOLD%"; \
	  BELOW=$$(echo "$$COVERAGE < $$THRESHOLD" | bc -l); \
	  if [ "$$BELOW" = "1" ]; then \
	    echo "  FAIL: $$COVERAGE_FMT% is BELOW threshold $$THRESHOLD%"; \
	    exit 1; \
	  fi; \
	  ABOVE=$$(echo "$$COVERAGE > $$THRESHOLD" | bc -l); \
	  if [ "$$ABOVE" = "1" ]; then \
	    NEW=$$(echo "$$COVERAGE" | awk '{print int($$1)}'); \
	    echo "  Ratcheting threshold: $$THRESHOLD% -> $$NEW%"; \
	    jq ".projects[\"$$proj\"] = $$NEW" coverage-thresholds.json > coverage-thresholds.json.tmp && mv coverage-thresholds.json.tmp coverage-thresholds.json; \
	  fi; \
	  echo "  PASS"; \
	done; \
	echo ""; \
	echo "==> All .NET test projects passed coverage thresholds."

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
	$(MAKE) _test_dotnet
	reportgenerator -reports:"**/TestResults/**/coverage.cobertura.xml" \
	  -targetdir:coverage/html -reporttypes:Html
ifeq ($(OS),Windows_NT)
	Start-Process coverage/html/index.html
else ifeq ($(shell uname -s),Darwin)
	open coverage/html/index.html
else
	xdg-open coverage/html/index.html
endif

_setup_dotnet:
	dotnet restore
	dotnet tool restore

# --- RUST (LQL LSP) ---
_build_rust:
	cd Lql/lql-lsp-rust && cargo build --release

_test_rust:
	@THRESHOLD=$$(jq -r '.projects["Lql/lql-lsp-rust"] // .default_threshold' coverage-thresholds.json); \
	echo ""; \
	echo "============================================================"; \
	echo "==> Testing Lql/lql-lsp-rust (threshold: $$THRESHOLD%)"; \
	echo "============================================================"; \
	cd Lql/lql-lsp-rust && cargo tarpaulin --workspace --skip-clean 2>&1 | tee /tmp/_dp_tarpaulin_out.txt; \
	TARP_EXIT=$${PIPESTATUS[0]}; \
	if [ $$TARP_EXIT -ne 0 ]; then \
	  echo "FAIL: cargo tarpaulin failed"; \
	  exit 1; \
	fi; \
	COVERAGE=$$(grep -oE '[0-9]+\.[0-9]+% coverage' /tmp/_dp_tarpaulin_out.txt | tail -1 | grep -oE '[0-9]+\.[0-9]+'); \
	if [ -z "$$COVERAGE" ]; then \
	  echo "FAIL: Could not parse coverage from tarpaulin output"; \
	  exit 1; \
	fi; \
	echo ""; \
	echo "  Coverage: $$COVERAGE% | Threshold: $$THRESHOLD%"; \
	BELOW=$$(echo "$$COVERAGE < $$THRESHOLD" | bc -l); \
	if [ "$$BELOW" = "1" ]; then \
	  echo "  FAIL: $$COVERAGE% is BELOW threshold $$THRESHOLD%"; \
	  exit 1; \
	fi; \
	ABOVE=$$(echo "$$COVERAGE > $$THRESHOLD" | bc -l); \
	if [ "$$ABOVE" = "1" ]; then \
	  NEW=$$(echo "$$COVERAGE" | awk '{print int($$1)}'); \
	  echo "  Ratcheting threshold: $$THRESHOLD% -> $$NEW%"; \
	  cd "$(CURDIR)" && jq '.projects["Lql/lql-lsp-rust"] = '"$$NEW" coverage-thresholds.json > coverage-thresholds.json.tmp && mv coverage-thresholds.json.tmp coverage-thresholds.json; \
	fi; \
	echo "  PASS"

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

_test_ts:
	@THRESHOLD=$$(jq -r '.projects["Lql/LqlExtension"] // .default_threshold' coverage-thresholds.json); \
	echo ""; \
	echo "============================================================"; \
	echo "==> Testing Lql/LqlExtension (threshold: $$THRESHOLD%)"; \
	echo "============================================================"; \
	cd Lql/LqlExtension && npm run test:coverage; \
	if [ $$? -ne 0 ]; then \
	  echo "FAIL: TypeScript extension tests failed"; \
	  exit 1; \
	fi; \
	SUMMARY="Lql/LqlExtension/coverage/coverage-summary.json"; \
	if [ ! -f "$$SUMMARY" ]; then \
	  SUMMARY="Lql/LqlExtension/.nyc_output/coverage-summary.json"; \
	fi; \
	if [ ! -f "$$SUMMARY" ]; then \
	  echo "FAIL: No coverage summary produced for Lql/LqlExtension"; \
	  exit 1; \
	fi; \
	COVERAGE=$$(jq -r '.total.lines.pct' "$$SUMMARY"); \
	echo ""; \
	echo "  Coverage: $$COVERAGE% | Threshold: $$THRESHOLD%"; \
	BELOW=$$(echo "$$COVERAGE < $$THRESHOLD" | bc -l); \
	if [ "$$BELOW" = "1" ]; then \
	  echo "  FAIL: $$COVERAGE% is BELOW threshold $$THRESHOLD%"; \
	  exit 1; \
	fi; \
	ABOVE=$$(echo "$$COVERAGE > $$THRESHOLD" | bc -l); \
	if [ "$$ABOVE" = "1" ]; then \
	  NEW=$$(echo "$$COVERAGE" | awk '{print int($$1)}'); \
	  echo "  Ratcheting threshold: $$THRESHOLD% -> $$NEW%"; \
	  jq '.projects["Lql/LqlExtension"] = '"$$NEW" coverage-thresholds.json > coverage-thresholds.json.tmp && mv coverage-thresholds.json.tmp coverage-thresholds.json; \
	fi; \
	echo "  PASS"

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
	@echo "  test           - Run full test suite with coverage enforcement"
	@echo "  lint           - Run all linters (errors mode)"
	@echo "  fmt            - Format all code in-place"
	@echo "  fmt-check      - Check formatting (no modification)"
	@echo "  clean          - Remove build artifacts"
	@echo "  check          - lint + test (pre-commit)"
	@echo "  ci             - lint + test + build (full CI)"
	@echo "  coverage       - Generate and open HTML coverage report"
	@echo "  setup          - Post-create dev environment setup"
