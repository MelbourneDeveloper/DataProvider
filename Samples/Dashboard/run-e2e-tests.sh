#!/bin/bash
set -e

# Dashboard E2E Test Script
# This script replicates the CI workflow for running Dashboard E2E tests locally

echo "=========================================="
echo "Dashboard E2E Test Runner"
echo "=========================================="

# Change to project root
cd "$(dirname "$0")/../.."

# Check if we're in the right directory
if [ ! -f "DataProvider.sln" ]; then
    echo "Error: Must run from project root or Samples/Dashboard directory"
    exit 1
fi

echo ""
echo "=== Step 1: Setup .NET ==="
dotnet --version

echo ""
echo "=== Step 2: Restore .NET tools ==="
dotnet tool restore

echo ""
echo "=== Step 3: Setup Node.js ==="
node --version || true
npm --version || true

echo ""
echo "=== Step 4: Restore NuGet packages ==="
dotnet restore Samples/Dashboard/Dashboard.Web
dotnet restore Samples/Dashboard/Dashboard.Integration.Tests

echo ""
echo "=== Step 5: Build Dashboard.Web (downloads React vendor files) ==="
dotnet build Samples/Dashboard/Dashboard.Web -c Release --no-restore

echo ""
echo "=== Step 6: Build Sync projects (required for Sync E2E tests) ==="
dotnet build Samples/Clinical/Clinical.Sync -c Release
dotnet build Samples/Scheduling/Scheduling.Sync -c Release

echo ""
echo "=== Step 7: Build ICD-10 API (required for ICD-10 E2E tests) ==="
dotnet build Samples/ICD10/ICD10.Api/ICD10.Api.csproj -c Release

echo ""
echo "=== Step 8: Build Integration Tests (includes wwwroot copy) ==="
dotnet build Samples/Dashboard/Dashboard.Integration.Tests -c Release

echo ""
echo "=== Step 9: Install Playwright browsers ==="
# Install Playwright CLI if not already installed
if ! command -v playwright &> /dev/null; then
    dotnet tool install --global Microsoft.Playwright.CLI || true
fi
# Install Chromium browser
playwright install --with-deps chromium 2>/dev/null || true

echo ""
echo "=== Step 10: Verify wwwroot files ==="
echo "=== Dashboard.Web wwwroot (source) ==="
ls -la Samples/Dashboard/Dashboard.Web/wwwroot/js/ 2>/dev/null || echo "Dashboard.Web js folder not found"
ls -la Samples/Dashboard/Dashboard.Web/wwwroot/js/vendor/ 2>/dev/null || echo "Dashboard.Web vendor folder not found"
echo "=== Integration Tests wwwroot (output) ==="
ls -la Samples/Dashboard/Dashboard.Integration.Tests/bin/Release/net10.0/wwwroot/js/ 2>/dev/null || echo "Integration Tests js folder not found"
ls -la Samples/Dashboard/Dashboard.Integration.Tests/bin/Release/net10.0/wwwroot/js/vendor/ 2>/dev/null || echo "Integration Tests vendor folder not found"

echo ""
echo "=== Step 11: Run E2E Tests ==="
dotnet test Samples/Dashboard/Dashboard.Integration.Tests -c Release --no-build --verbosity normal

echo ""
echo "=========================================="
echo "E2E Tests Complete"
echo "=========================================="
