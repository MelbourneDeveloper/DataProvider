# DataProvider Logging Implementation Plan

## Overview

This document provides a concrete implementation plan for adding logging and observability to the DataProvider code generation system. It includes step-by-step instructions, code examples, and integration patterns.

## Implementation Approach

Based on the project's functional programming principles and the requirement to avoid breaking changes, we'll implement logging as:

1. **Optional parameters** in generated methods
2. **Enhanced code templates** that support logging
3. **Example implementations** showing different logging patterns
4. **Zero-overhead design** when logging is disabled

## Phase 1: Core Infrastructure

### 1.1 Create LoggingCodeTemplate Base Class

Create a new file: `DataProvider/DataProvider/CodeGeneration/LoggingCodeTemplate.cs`

```csharp
using System.Globalization;
using System.Text;
using Results;
using Selecta;

namespace DataProvider.CodeGeneration;

/// <summary>
/// Enhanced code template that supports optional logging injection
/// </summary>
public class LoggingCodeTemplate : DefaultCodeTemplate
{
    /// <summary>
    /// Whether to generate methods with optional ILogger parameters
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Whether to log SQL parameters (with masking for sensitive data)
    /// </summary>
    public bool LogParameters { get; set; } = true;

    /// <summary>
    /// Whether to log execution timing information
    /// </summary>
    public bool LogTiming { get; set; } = true;

    /// <summary>
    /// Whether to log result set information (row counts, etc.)
    /// </summary>
    public bool LogResults { get; set; } = true;

    /// <summary>
    /// Whether to log only errors (minimal logging mode)
    /// </summary>
    public bool ErrorOnlyLogging { get; set; } = false;

    /// <summary>
    /// Generates the data access extension method with optional logging
    /// </summary>
    public override Result<string, SqlError> GenerateDataAccessMethod(
        string methodName,
        string returnTypeName,
        string sql,
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<DatabaseColumn> columns
    )
    {
        if (!EnableLogging)
            return base.GenerateDataAccessMethod(methodName, returnTypeName, sql, parameters, columns);

        var className = string.Create(CultureInfo.InvariantCulture, $"{methodName}Extensions");
        return GenerateQueryMethodWithLogging(
            className,
            methodName,
            returnTypeName,
            sql,
            parameters,
            columns,
            "SqliteConnection"
        );
    }

    /// <summary>
    /// Generates a query method with integrated logging support
    /// </summary>
    private Result<string, SqlError> GenerateQueryMethodWithLogging(
        string className,
        string methodName,
        string returnTypeName,
        string sql,
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<DatabaseColumn> columns,
        string connectionType
    )
    {
        if (string.IsNullOrWhiteSpace(className))
            return new Result<string, SqlError>.Failure(new SqlError("className cannot be null or empty"));

        if (string.IsNullOrWhiteSpace(methodName))
            return new Result<string, SqlError>.Failure(new SqlError("methodName cannot be null or empty"));

        if (string.IsNullOrWhiteSpace(returnTypeName))
            return new Result<string, SqlError>.Failure(new SqlError("returnTypeName cannot be null or empty"));

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<string, SqlError>.Failure(new SqlError("sql cannot be null or empty"));

        if (columns == null || columns.Count == 0)
            return new Result<string, SqlError>.Failure(new SqlError("columns cannot be null or empty"));

        var sb = new StringBuilder();

        // Generate using statements for logging
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine("using System.Diagnostics;");
        sb.AppendLine();

        // Generate extension class
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Extension methods for '{methodName}' with optional logging support.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static partial class {className}");
        sb.AppendLine("{");

        // Generate method signature with optional logger parameter
        var parameterList = GenerateParameterListWithLogger(parameters);
        
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Executes '{methodName}.sql' and maps results with optional logging.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"connection\">Open {connectionType} connection.</param>");

        if (parameters != null)
        {
            foreach (var p in parameters)
            {
                sb.AppendLine($"    /// <param name=\"{p.Name}\">Query parameter.</param>");
            }
        }

        sb.AppendLine("    /// <param name=\"logger\">Optional logger for operation tracking.</param>");
        sb.AppendLine("    /// <returns>Result of records or SQL error.</returns>");
        sb.AppendLine($"    public static async Task<Result<ImmutableList<{returnTypeName}>, SqlError>> {methodName}Async(this {connectionType} connection{parameterList})");
        sb.AppendLine("    {");

        // Generate method body with logging
        GenerateMethodBodyWithLogging(sb, methodName, sql, returnTypeName, parameters, columns, connectionType);

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new Result<string, SqlError>.Success(sb.ToString());
    }

    /// <summary>
    /// Generates parameter list including optional logger parameter
    /// </summary>
    private string GenerateParameterListWithLogger(IReadOnlyList<ParameterInfo> parameters)
    {
        var paramList = new List<string>();
        
        if (parameters != null)
        {
            paramList.AddRange(parameters.Select(p => $"object {p.Name}"));
        }
        
        paramList.Add("ILogger? logger = null");
        
        return paramList.Count > 0 ? ", " + string.Join(", ", paramList) : "";
    }

    /// <summary>
    /// Generates the method body with integrated logging calls
    /// </summary>
    private void GenerateMethodBodyWithLogging(
        StringBuilder sb,
        string methodName,
        string sql,
        string returnTypeName,
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<DatabaseColumn> columns,
        string connectionType)
    {
        // SQL constant
        sb.AppendLine($"        const string sql = @\"{sql.Replace("\"", "\"\"", StringComparison.Ordinal)}\";");
        sb.AppendLine();

        // Logging scope and timing setup
        sb.AppendLine("        using var activity = logger?.BeginScope(new Dictionary<string, object>");
        sb.AppendLine("        {");
        sb.AppendLine($"            [\"Method\"] = \"{methodName}\",");
        sb.AppendLine("            [\"Operation\"] = \"DatabaseQuery\"");
        sb.AppendLine("        });");
        sb.AppendLine();

        if (!ErrorOnlyLogging)
        {
            sb.AppendLine($"        logger?.LogInformation(\"Executing {methodName} query\");");
        }

        if (LogTiming)
        {
            sb.AppendLine("        var stopwatch = logger != null ? Stopwatch.StartNew() : null;");
        }

        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine($"            var results = ImmutableList.CreateBuilder<{returnTypeName}>();");
        sb.AppendLine();

        var commandType = connectionType.Replace("Connection", "Command", StringComparison.Ordinal);
        sb.AppendLine($"            using (var command = new {commandType}(sql, connection))");
        sb.AppendLine("            {");

        // Add parameters with optional logging
        if (parameters != null && parameters.Count > 0)
        {
            foreach (var parameter in parameters)
            {
                sb.AppendLine($"                command.Parameters.AddWithValue(\"@{parameter.Name}\", {parameter.Name} ?? (object)DBNull.Value);");
            }

            if (LogParameters && !ErrorOnlyLogging)
            {
                sb.AppendLine();
                sb.AppendLine("                if (logger?.IsEnabled(LogLevel.Debug) == true)");
                sb.AppendLine("                {");
                foreach (var parameter in parameters)
                {
                    sb.AppendLine($"                    logger.LogDebug(\"Parameter @{parameter.Name} = {{Value}}\", {parameter.Name});");
                }
                sb.AppendLine("                }");
            }
        }

        sb.AppendLine();
        sb.AppendLine("                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))");
        sb.AppendLine("                {");
        sb.AppendLine("                    while (await reader.ReadAsync().ConfigureAwait(false))");
        sb.AppendLine("                    {");

        // Generate record constructor
        sb.AppendLine($"                        var item = new {returnTypeName}(");
        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var isLast = i == columns.Count - 1;
            var comma = isLast ? "" : ",";

            sb.AppendLine($"                            reader.IsDBNull({i}) ? {(column.IsNullable ? "null" : $"default({column.CSharpType})")} : ({column.CSharpType})reader.GetValue({i}){comma}");
        }
        sb.AppendLine("                        );");
        sb.AppendLine("                        results.Add(item);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();

        // Success logging
        if (LogTiming || LogResults)
        {
            sb.AppendLine("            var resultCount = results.Count;");
            if (!ErrorOnlyLogging)
            {
                sb.AppendLine("            if (logger?.IsEnabled(LogLevel.Information) == true)");
                sb.AppendLine("            {");
                if (LogTiming && LogResults)
                {
                    sb.AppendLine($"                logger.LogInformation(\"{methodName} completed in {{ElapsedMs}}ms, returned {{Count}} records\",");
                    sb.AppendLine("                    stopwatch?.ElapsedMilliseconds ?? 0, resultCount);");
                }
                else if (LogTiming)
                {
                    sb.AppendLine($"                logger.LogInformation(\"{methodName} completed in {{ElapsedMs}}ms\",");
                    sb.AppendLine("                    stopwatch?.ElapsedMilliseconds ?? 0);");
                }
                else if (LogResults)
                {
                    sb.AppendLine($"                logger.LogInformation(\"{methodName} returned {{Count}} records\", resultCount);");
                }
                sb.AppendLine("            }");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"            return new Result<ImmutableList<{returnTypeName}>, SqlError>.Success(results.ToImmutable());");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");

        // Error logging
        if (LogTiming)
        {
            sb.AppendLine($"            logger?.LogError(ex, \"{methodName} failed after {{ElapsedMs}}ms\",");
            sb.AppendLine("                stopwatch?.ElapsedMilliseconds ?? 0);");
        }
        else
        {
            sb.AppendLine($"            logger?.LogError(ex, \"{methodName} failed\");");
        }

        sb.AppendLine($"            return new Result<ImmutableList<{returnTypeName}>, SqlError>.Failure(new SqlError(\"Database error\", ex));");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Generates the complete source file with logging support
    /// </summary>
    public override Result<string, SqlError> GenerateSourceFile(
        string namespaceName,
        string modelCode,
        string dataAccessCode
    )
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
            return new Result<string, SqlError>.Failure(new SqlError("namespaceName cannot be null or empty"));

        if (string.IsNullOrWhiteSpace(modelCode) && string.IsNullOrWhiteSpace(dataAccessCode))
            return new Result<string, SqlError>.Failure(new SqlError("At least one of modelCode or dataAccessCode must be provided"));

        var sb = new StringBuilder();

        // Generate using statements (including logging)
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Microsoft.Data.Sqlite;");
        sb.AppendLine("using Results;");
        
        if (EnableLogging)
        {
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using System.Diagnostics;");
        }
        
        sb.AppendLine();

        // Generate namespace
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();

        // Add data access code if provided
        if (!string.IsNullOrWhiteSpace(dataAccessCode))
        {
            sb.AppendLine(dataAccessCode);
            sb.AppendLine();
        }

        // Add model code if provided
        if (!string.IsNullOrWhiteSpace(modelCode))
        {
            sb.Append(modelCode);
        }

        return new Result<string, SqlError>.Success(sb.ToString());
    }
}
```

### 1.2 Create Example Templates

Create `DataProvider/DataProvider/CodeGeneration/Examples/` directory with example templates:

#### BasicLoggingTemplate.cs
```csharp
using Results;
using Selecta;

namespace DataProvider.CodeGeneration.Examples;

/// <summary>
/// Simple logging template with basic start/end logging
/// </summary>
public class BasicLoggingTemplate : LoggingCodeTemplate
{
    public BasicLoggingTemplate()
    {
        EnableLogging = true;
        LogParameters = false;
        LogTiming = true;
        LogResults = true;
        ErrorOnlyLogging = false;
    }
}
```

#### PerformanceLoggingTemplate.cs
```csharp
using Results;
using Selecta;

namespace DataProvider.CodeGeneration.Examples;

/// <summary>
/// Performance-focused logging template with detailed timing and metrics
/// </summary>
public class PerformanceLoggingTemplate : LoggingCodeTemplate
{
    public PerformanceLoggingTemplate()
    {
        EnableLogging = true;
        LogParameters = true;
        LogTiming = true;
        LogResults = true;
        ErrorOnlyLogging = false;
    }
}
```

#### ErrorOnlyLoggingTemplate.cs
```csharp
using Results;
using Selecta;

namespace DataProvider.CodeGeneration.Examples;

/// <summary>
/// Minimal logging template that only logs errors
/// </summary>
public class ErrorOnlyLoggingTemplate : LoggingCodeTemplate
{
    public ErrorOnlyLoggingTemplate()
    {
        EnableLogging = true;
        LogParameters = false;
        LogTiming = true;
        LogResults = false;
        ErrorOnlyLogging = true;
    }
}
```

## Phase 2: Integration Examples

### 2.1 Create Example Project with Logging

Create `DataProvider/DataProvider.LoggingExample/` directory:

#### DataProvider.LoggingExample.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../DataProvider/DataProvider.csproj" />
    <ProjectReference Include="../DataProvider.SQLite/DataProvider.SQLite.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
  </ItemGroup>

</Project>
```

#### Program.cs
```csharp
using DataProvider.LoggingExample;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();

// Configure logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

// Add services
services.AddTransient<CustomerService>();
services.AddTransient<DatabaseService>();

var serviceProvider = services.BuildServiceProvider();

// Run examples
var customerService = serviceProvider.GetRequiredService<CustomerService>();
var databaseService = serviceProvider.GetRequiredService<DatabaseService>();

await databaseService.InitializeDatabaseAsync();
await customerService.DemonstrateLoggingAsync();

Console.WriteLine("Logging examples completed!");
```

#### CustomerService.cs
```csharp
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Generated; // Generated extension methods

namespace DataProvider.LoggingExample;

/// <summary>
/// Service demonstrating logging integration with generated methods
/// </summary>
public class CustomerService
{
    private readonly ILogger<CustomerService> _logger;
    private readonly DatabaseService _databaseService;

    public CustomerService(ILogger<CustomerService> logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    /// <summary>
    /// Demonstrates various logging patterns with generated methods
    /// </summary>
    public async Task DemonstrateLoggingAsync()
    {
        using var connection = await _databaseService.GetConnectionAsync();

        _logger.LogInformation("Starting customer service demonstration");

        // Example 1: Basic logging
        await DemonstrateBasicLoggingAsync(connection);

        // Example 2: Structured logging with correlation ID
        await DemonstrateStructuredLoggingAsync(connection);

        // Example 3: Error handling with logging
        await DemonstrateErrorHandlingAsync(connection);

        // Example 4: Performance logging
        await DemonstratePerformanceLoggingAsync(connection);

        _logger.LogInformation("Customer service demonstration completed");
    }

    private async Task DemonstrateBasicLoggingAsync(SqliteConnection connection)
    {
        _logger.LogInformation("=== Basic Logging Example ===");

        // Generated method with logger parameter
        var result = await connection.GetCustomersAsync(isActive: true, logger: _logger);

        result.Match(
            success => _logger.LogInformation("Successfully retrieved {Count} customers", success.Count),
            error => _logger.LogError("Failed to retrieve customers: {Error}", error.Message)
        );
    }

    private async Task DemonstrateStructuredLoggingAsync(SqliteConnection connection)
    {
        _logger.LogInformation("=== Structured Logging Example ===");

        var correlationId = Guid.NewGuid().ToString();
        
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["Operation"] = "GetActiveCustomers"
        });

        var result = await connection.GetCustomersAsync(isActive: true, logger: _logger);

        result.Match(
            success => _logger.LogInformation("Operation completed successfully with {Count} results", success.Count),
            error => _logger.LogError("Operation failed: {Error}", error.Message)
        );
    }

    private async Task DemonstrateErrorHandlingAsync(SqliteConnection connection)
    {
        _logger.LogInformation("=== Error Handling Example ===");

        try
        {
            // This will demonstrate error logging when something goes wrong
            var result = await connection.GetOrdersAsync(
                customerId: 999999, // Non-existent customer
                status: "Active",
                startDate: "2024-01-01",
                endDate: "2024-12-31",
                logger: _logger
            );

            result.Match(
                success => _logger.LogInformation("Retrieved {Count} orders", success.Count),
                error => _logger.LogWarning("No orders found or query failed: {Error}", error.Message)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during order retrieval");
        }
    }

    private async Task DemonstratePerformanceLoggingAsync(SqliteConnection connection)
    {
        _logger.LogInformation("=== Performance Logging Example ===");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Multiple queries to demonstrate performance logging
        var tasks = new[]
        {
            connection.GetCustomersAsync(isActive: true, logger: _logger),
            connection.GetOrdersAsync(customerId: 1, status: "Completed", startDate: "2024-01-01", endDate: "2024-12-31", logger: _logger),
            connection.GetInvoicesAsync(customerName: "Acme Corp", startDate: "2024-01-01", endDate: "2024-12-31", logger: _logger)
        };

        await Task.WhenAll(tasks);

        stopwatch.Stop();
        _logger.LogInformation("All queries completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
    }
}
```

#### DatabaseService.cs
```csharp
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace DataProvider.LoggingExample;

/// <summary>
/// Service for database operations and initialization
/// </summary>
public class DatabaseService
{
    private readonly ILogger<DatabaseService> _logger;
    private const string ConnectionString = "Data Source=:memory:";

    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initializes the in-memory database with sample data
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        _logger.LogInformation("Initializing database");

        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        // Create tables
        await CreateTablesAsync(connection);
        
        // Seed data
        await SeedDataAsync(connection);

        _logger.LogInformation("Database initialized successfully");
    }

    /// <summary>
    /// Gets a new database connection
    /// </summary>
    public async Task<SqliteConnection> GetConnectionAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task CreateTablesAsync(SqliteConnection connection)
    {
        var createCustomerTable = @"
            CREATE TABLE Customer (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerName TEXT NOT NULL,
                Email TEXT,
                Phone TEXT,
                CreatedDate TEXT NOT NULL
            )";

        var createOrderTable = @"
            CREATE TABLE Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderNumber TEXT NOT NULL,
                OrderDate TEXT NOT NULL,
                CustomerId INTEGER NOT NULL,
                TotalAmount REAL NOT NULL,
                Status TEXT NOT NULL,
                FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
            )";

        var createInvoiceTable = @"
            CREATE TABLE Invoice (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                InvoiceNumber TEXT NOT NULL,
                InvoiceDate TEXT NOT NULL,
                CustomerName TEXT NOT NULL,
                TotalAmount REAL NOT NULL
            )";

        using var command = connection.CreateCommand();
        
        command.CommandText = createCustomerTable;
        await command.ExecuteNonQueryAsync();
        
        command.CommandText = createOrderTable;
        await command.ExecuteNonQueryAsync();
        
        command.CommandText = createInvoiceTable;
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedDataAsync(SqliteConnection connection)
    {
        var customers = new[]
        {
            ("Acme Corp", "contact@acme.com", "555-0001", "2024-01-01"),
            ("TechStart Inc", "hello@techstart.com", "555-0002", "2024-01-15"),
            ("Global Solutions", "info@global.com", "555-0003", "2024-02-01")
        };

        foreach (var (name, email, phone, date) in customers)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Customer (CustomerName, Email, Phone, CreatedDate) VALUES (@name, @email, @phone, @date)";
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@email", email);
            command.Parameters.AddWithValue("@phone", phone);
            command.Parameters.AddWithValue("@date", date);
            await command.ExecuteNonQueryAsync();
        }

        // Add sample orders and invoices...
        _logger.LogDebug("Sample data seeded successfully");
    }
}
```

## Phase 3: Documentation and Examples

### 3.1 Create Comprehensive Documentation

#### LOGGING_INTEGRATION_GUIDE.md
```markdown
# DataProvider Logging Integration Guide

## Quick Start

### 1. Enable Logging in Your Code Generator

```csharp
// Use the logging-enabled template
var template = new LoggingCodeTemplate
{
    EnableLogging = true,
    LogTiming = true,
    LogParameters = true,
    LogResults = true
};

// Generate code with logging support
var generator = new SqliteCodeGenerator(template);
```

### 2. Use Generated Methods with Logging

```csharp
// Inject ILogger into your service
public class CustomerService
{
    private readonly ILogger<CustomerService> _logger;
    private readonly SqliteConnection _connection;

    public CustomerService(ILogger<CustomerService> logger, SqliteConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public async Task<List<Customer>> GetActiveCustomersAsync()
    {
        // Pass logger to generated method
        var result = await _connection.GetCustomersAsync(isActive: true, logger: _logger);
        
        return result.Match(
            success => success.ToList(),
            error => throw new InvalidOperationException(error.Message)
        );
    }
}
```

### 3. Configure Logging in Your Application

```csharp
// In Program.cs or Startup.cs
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddDebug();
    builder.SetMinimumLevel(LogLevel.Information);
});
```

## Advanced Scenarios

### Custom Logging Templates

Create your own logging template by inheriting from `LoggingCodeTemplate`:

```csharp
public class MyCustomLoggingTemplate : LoggingCodeTemplate
{
    public MyCustomLoggingTemplate()
    {
        EnableLogging = true;
        LogParameters = false; // Don't log parameters for security
        LogTiming = true;
        LogResults = false;
        ErrorOnlyLogging = false;
    }

    // Override methods to customize logging behavior
}
```

### Structured Logging with Context

```csharp
public async Task ProcessOrderAsync(int orderId)
{
    using var scope = _logger.BeginScope(new Dictionary<string, object>
    {
        ["OrderId"] = orderId,
        ["Operation"] = "ProcessOrder",
        ["CorrelationId"] = HttpContext.TraceIdentifier
    });

    var result = await _connection.GetOrderAsync(orderId, logger: _logger);
    // ... process order
}
```

### Performance Monitoring

```csharp
public async Task<PerformanceMetrics> GetPerformanceMetricsAsync()
{
    var stopwatch = Stopwatch.StartNew();
    
    var result = await _connection.GetLargeDataSetAsync(logger: _logger);
    
    stopwatch.Stop();
    
    return new PerformanceMetrics
    {
        ExecutionTime = stopwatch.Elapsed,
        RecordCount = result.IsSuccess ? result.Value.Count : 0
    };
}
```

## Logging Levels and Configuration

### Recommended Log Levels

- **Information**: Query start/completion, record counts
- **Debug**: Parameter values, detailed execution steps
- **Warning**: Performance issues, unexpected results
- **Error**: Database errors, connection failures

### Configuration Examples

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "DataProvider": "Debug",
      "Generated": "Information"
    }
  }
}
```

## Best Practices

1. **Use structured logging** with consistent property names
2. **Include correlation IDs** for request tracing
3. **Log performance metrics** for slow query detection
4. **Mask sensitive parameters** in debug logs
5. **Use appropriate log levels** to control verbosity
6. **Include error context** when logging failures

## Troubleshooting

### Common Issues

**Issue**: Logger parameter not available in generated methods
**Solution**: Ensure you're using `LoggingCodeTemplate` with `EnableLogging = true`

**Issue**: No log output appearing
**Solution**: Check your logging configuration and minimum log levels

**Issue**: Performance impact from logging
**Solution**: Use `ErrorOnlyLoggingTemplate` or disable logging in production

### Debugging Tips

1. Enable debug logging to see parameter values
2. Use structured logging to filter by operation type
3. Include timing information to identify slow queries
4. Add correlation IDs to trace requests across services
```

## Phase 4: Testing Strategy

### 4.1 Unit Tests for Logging Templates

Create `DataProvider/DataProvider.Tests/LoggingCodeTemplateTests.cs`:

```csharp
using DataProvider.CodeGeneration;
using FluentAssertions;
using Results;
using Selecta;
using Xunit;

namespace DataProvider.Tests;

/// <summary>
/// Tests for logging code template functionality
/// </summary>
public class LoggingCodeTemplateTests
{
    [Fact]
    public void GenerateDataAccessMethod_WithLoggingEnabled_ShouldIncludeLoggerParameter()
    {
        // Arrange
        var template = new LoggingCodeTemplate { EnableLogging = true };
        var parameters = new List<ParameterInfo>
        {
            new("customerId", "int")
        };
        var columns = new List<DatabaseColumn>
        {
            new("Id", "INTEGER", "long", false),
            new("Name", "TEXT", "string", false)
        };

        // Act
        var result = template.GenerateDataAccessMethod(
            "GetCustomers",
            "Customer",
            "SELECT Id, Name FROM Customer WHERE Id = @customerId",
            parameters,
            columns
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("ILogger? logger = null");
        result.Value.Should().Contain("logger?.LogInformation");
        result.Value.Should().Contain("logger?.LogError");
    }

    [Fact]
    public void GenerateDataAccessMethod_WithLoggingDisabled_ShouldNotIncludeLoggerParameter()
    {
        // Arrange
        var template = new LoggingCodeTemplate { EnableLogging = false };
        var parameters = new List<ParameterInfo>();
        var columns = new List<DatabaseColumn>
        {
            new("Id", "INTEGER", "long", false)
        };

        // Act
        var result = template.GenerateDataAccessMethod(
            "GetCustomers",
            "Customer",
            "SELECT Id FROM Customer",
            parameters,
            columns
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotContain("ILogger");
        result.Value.Should().NotContain("logger?.Log");
    }

    [Fact]
    public void GenerateDataAccessMethod_WithErrorOnlyLogging_ShouldOnlyLogErrors()
    {
        // Arrange
        var template = new LoggingCodeTemplate 
        { 
            EnableLogging = true,
            ErrorOnlyLogging = true
        };
        var parameters = new List<ParameterInfo>();
        var columns = new List<DatabaseColumn>
        {
            new("Id", "INTEGER", "long", false)
        };

        // Act
        var result = template.GenerateDataAccessMethod(
            "GetCustomers",
            "Customer",
            "SELECT Id FROM Customer",
            parameters,
            columns
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("logger?.LogError");
        result.Value.Should().NotContain("logger?.LogInformation");
    }
}
```

### 4.2 Integration Tests

Create `DataProvider/DataProvider.Tests/LoggingIntegrationTests.cs`:

```csharp
using DataProvider.CodeGeneration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataProvider.Tests;

/// <summary>
/// Integration tests for logging functionality
/// </summary>
public class LoggingIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FakeLogger<LoggingIntegrationTests> _logger;

    public LoggingIntegrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        
        _logger = new FakeLogger<LoggingIntegrationTests>();
        
        // Setup test database
        SetupDatabase();
    }

    [Fact]
    public async Task GeneratedMethod_WithLogger_ShouldLogQueryExecution()
    {
        // This test would use generated code with logging
        // and verify that appropriate log entries are created
        
        // Arrange - would use generated extension method
        // Act - call generated method with logger
        // Assert - verify log entries
        
        Assert.True(true); // Placeholder for actual implementation
    }

    [Fact]
    public async Task GeneratedMethod_WithError_ShouldLogError()
    {
        // Test error logging scenarios
        Assert.True(true); // Placeholder for actual implementation
    }

    private void SetupDatabase()
    {
        var createTable = @"
            CREATE TABLE Customer (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL
            )";

        using var command = _connection.CreateCommand();
        command.CommandText = createTable;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
```

## Implementation Timeline

### Week 1-2: Core Infrastructure
- [ ] Create `LoggingCodeTemplate` class
- [ ] Implement optional `ILogger` parameter injection
- [ ] Add basic logging to generated methods
- [ ] Write unit tests for logging templates
- [ ] Ensure backward compatibility

### Week 3-4: Enhanced Features
- [ ] Add structured logging support
- [ ] Implement performance timing
- [ ] Add parameter logging with masking
- [ ] Create example logging templates
- [ ] Write integration tests

### Week 5-6: Documentation and Examples
- [ ] Create comprehensive documentation
- [ ] Build example project with logging
- [ ] Write troubleshooting guide
- [ ] Create video tutorials
- [ ] Update existing examples

## Success Metrics

### Technical Metrics
- [ ] Zero performance impact when logging disabled (benchmark)
- [ ] <5% performance impact when logging enabled
- [ ] 100% backward compatibility (no breaking changes)
- [ ] >90% test coverage for logging components

### User Experience Metrics
- [ ] Documentation completeness score >95%
- [ ] Example project runs successfully
- [ ] Clear integration path (5 steps or less)
- [ ] Positive community feedback

## Conclusion

This implementation plan provides a comprehensive approach to adding logging and observability to the DataProvider system while maintaining its core principles of performance, type safety, and functional programming. The phased approach ensures that each component is thoroughly tested and documented before moving to the next phase.

The key to success will be maintaining the optional nature of logging while providing compelling examples that demonstrate its value for production applications.
