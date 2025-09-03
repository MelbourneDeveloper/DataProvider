# Product Requirements Document: Logging and Observability for DataProvider

## Executive Summary

This PRD outlines the requirements for adding logging and observability capabilities to the DataProvider code generation system. The goal is to provide developers with optional, non-intrusive logging capabilities that can be injected into generated methods while maintaining the project's functional programming principles and zero-overhead philosophy.

## Background

The DataProvider project generates compile-time safe database access methods from SQL files. Currently, there is no standardized way to add logging or observability to these generated methods. Developers need:

1. **Optional logging injection** into generated methods
2. **Examples and patterns** for custom logging implementations
3. **Non-intrusive approach** that doesn't break existing code
4. **Performance-conscious design** that maintains zero-overhead when logging is disabled

## Problem Statement

### Current State
- Generated methods have no logging capabilities
- No standard way to inject `ILogger` into generated extension methods
- No examples of how to add observability to custom templates
- Developers must manually wrap generated methods to add logging

### Pain Points
1. **No visibility** into database operation performance and errors
2. **Difficult debugging** when database operations fail
3. **No standardized logging patterns** for generated code
4. **Manual effort required** to add logging to every generated method

## Goals and Objectives

### Primary Goals
1. **Provide optional logging injection** mechanism for generated methods
2. **Create comprehensive examples** showing logging integration patterns
3. **Maintain backward compatibility** with existing generated code
4. **Preserve performance characteristics** when logging is disabled

### Secondary Goals
1. **Support multiple logging frameworks** (Microsoft.Extensions.Logging, Serilog, NLog)
2. **Enable structured logging** with contextual information
3. **Provide observability metrics** (timing, error rates, query performance)
4. **Create reusable logging templates** for common scenarios

### Non-Goals
1. **Making logging mandatory** - it should remain completely optional
2. **Adding runtime overhead** when logging is disabled
3. **Breaking existing generated code** or changing current APIs
4. **Implementing a custom logging framework** - leverage existing ones

## Success Criteria

### Must Have
- [ ] Generated methods can optionally accept `ILogger` parameter
- [ ] Example templates showing `ILogger` injection patterns
- [ ] Documentation with complete logging integration examples
- [ ] Zero performance impact when logging is disabled
- [ ] Backward compatibility with existing generated code

### Should Have
- [ ] Structured logging with query context (SQL, parameters, timing)
- [ ] Error logging with detailed exception information
- [ ] Performance metrics logging (execution time, row counts)
- [ ] Multiple logging framework examples

### Could Have
- [ ] OpenTelemetry integration examples
- [ ] Metrics collection for observability dashboards
- [ ] Automatic correlation ID generation
- [ ] Query performance analysis helpers

## Requirements

### Functional Requirements

#### FR1: Optional ILogger Parameter Injection
- Generated methods should optionally accept an `ILogger` parameter
- When provided, the logger should be used for operation logging
- When not provided, no logging overhead should occur
- Logger parameter should be the last parameter in method signatures

#### FR2: Logging Template Examples
- Provide example implementations of `ICodeTemplate` with logging
- Show how to inject `ILogger` into generated extension methods
- Demonstrate structured logging with query context
- Include error handling and exception logging patterns

#### FR3: Performance Logging
- Log query execution start/completion with timing
- Log parameter values (with optional sensitive data masking)
- Log result set sizes and performance metrics
- Support configurable log levels for different scenarios

#### FR4: Error Logging
- Log SQL errors with full context (query, parameters, exception)
- Include correlation IDs for tracing requests
- Provide detailed error information for debugging
- Support error classification and categorization

#### FR5: Custom Template Integration
- Show how to extend `DefaultCodeTemplate` with logging
- Provide base classes for common logging scenarios
- Support custom logging implementations
- Enable per-method logging configuration

### Non-Functional Requirements

#### NFR1: Performance
- Zero overhead when logging is disabled
- Minimal allocation impact when logging is enabled
- Support async logging to avoid blocking database operations
- Configurable logging levels to control verbosity

#### NFR2: Compatibility
- Maintain full backward compatibility with existing code
- Support .NET 8+ logging abstractions
- Work with dependency injection containers
- Compatible with existing Result<T> error handling

#### NFR3: Usability
- Clear documentation with step-by-step examples
- IntelliSense support for logger parameters
- Consistent naming conventions
- Easy integration with existing applications

## Technical Design

### Architecture Overview

The logging system will be implemented as:

1. **Optional Template Extensions**: Enhanced code templates that generate methods with optional `ILogger` parameters
2. **Example Implementations**: Concrete examples showing different logging patterns
3. **Documentation**: Comprehensive guides for implementing custom logging
4. **Helper Utilities**: Common logging utilities and patterns

### Key Components

#### 1. LoggingCodeTemplate
```csharp
public class LoggingCodeTemplate : DefaultCodeTemplate
{
    public bool EnableLogging { get; set; } = false;
    public bool LogParameters { get; set; } = true;
    public bool LogTiming { get; set; } = true;
    public bool LogResults { get; set; } = false;
}
```

#### 2. Enhanced Method Generation
Generated methods will optionally include:
- `ILogger? logger = null` parameter
- Logging statements for operation start/completion
- Error logging with full context
- Performance timing measurements

#### 3. Example Templates
- `BasicLoggingTemplate`: Simple logging with start/end
- `StructuredLoggingTemplate`: Rich structured logging with context
- `PerformanceLoggingTemplate`: Focus on timing and metrics
- `ErrorOnlyLoggingTemplate`: Only log errors and exceptions

### Implementation Strategy

#### Phase 1: Core Logging Infrastructure
1. Create `LoggingCodeTemplate` base class
2. Implement optional `ILogger` parameter injection
3. Add basic start/completion logging
4. Ensure zero overhead when disabled

#### Phase 2: Enhanced Logging Features
1. Add structured logging with query context
2. Implement performance timing
3. Add error logging with detailed context
4. Create parameter logging with masking

#### Phase 3: Examples and Documentation
1. Create comprehensive example templates
2. Write integration documentation
3. Provide sample applications
4. Create troubleshooting guides

## Examples and Use Cases

### Example 1: Basic Logging Integration

```csharp
// Generated method with optional logger
public static async Task<Result<ImmutableList<Customer>, SqlError>> GetCustomersAsync(
    this SqliteConnection connection,
    bool isActive,
    ILogger? logger = null)
{
    using var activity = logger?.BeginScope("GetCustomers");
    logger?.LogInformation("Executing GetCustomers query with isActive={IsActive}", isActive);
    
    var stopwatch = logger != null ? Stopwatch.StartNew() : null;
    
    try
    {
        // ... existing generated code ...
        
        logger?.LogInformation("GetCustomers completed in {ElapsedMs}ms, returned {Count} records", 
            stopwatch?.ElapsedMilliseconds, result.Count);
        
        return new Result<ImmutableList<Customer>, SqlError>.Success(result);
    }
    catch (Exception ex)
    {
        logger?.LogError(ex, "GetCustomers failed after {ElapsedMs}ms", 
            stopwatch?.ElapsedMilliseconds);
        return new Result<ImmutableList<Customer>, SqlError>.Failure(new SqlError("Query failed", ex));
    }
}
```

### Example 2: Custom Logging Template

```csharp
public class CustomLoggingTemplate : LoggingCodeTemplate
{
    public override Result<string, SqlError> GenerateDataAccessMethod(
        string methodName,
        string returnTypeName,
        string sql,
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<DatabaseColumn> columns)
    {
        // Generate method with custom logging behavior
        var baseMethod = base.GenerateDataAccessMethod(methodName, returnTypeName, sql, parameters, columns);
        
        if (!EnableLogging)
            return baseMethod;
            
        // Add custom logging enhancements
        return EnhanceWithLogging(baseMethod.Value, methodName, parameters);
    }
}
```

### Example 3: Usage in Application

```csharp
// In your application
public class CustomerService
{
    private readonly SqliteConnection _connection;
    private readonly ILogger<CustomerService> _logger;
    
    public CustomerService(SqliteConnection connection, ILogger<CustomerService> logger)
    {
        _connection = connection;
        _logger = logger;
    }
    
    public async Task<List<Customer>> GetActiveCustomersAsync()
    {
        var result = await _connection.GetCustomersAsync(isActive: true, logger: _logger);
        
        return result switch
        {
            Result<ImmutableList<Customer>, SqlError>.Success success => success.Value.ToList(),
            Result<ImmutableList<Customer>, SqlError>.Failure failure => throw new InvalidOperationException(failure.ErrorValue.Message),
            _ => throw new InvalidOperationException("Unknown result type")
        };
    }
}
```

## Implementation Plan

### Phase 1: Foundation (Week 1-2)
- [ ] Create `LoggingCodeTemplate` base class
- [ ] Implement optional `ILogger` parameter injection
- [ ] Add basic logging to `DataAccessGenerator`
- [ ] Ensure backward compatibility
- [ ] Write unit tests for logging templates

### Phase 2: Enhanced Features (Week 3-4)
- [ ] Add structured logging with query context
- [ ] Implement performance timing measurements
- [ ] Add parameter logging with sensitive data masking
- [ ] Create error logging with detailed context
- [ ] Add integration tests

### Phase 3: Examples and Documentation (Week 5-6)
- [ ] Create example logging templates
- [ ] Write comprehensive documentation
- [ ] Create sample application demonstrating logging
- [ ] Add troubleshooting guide
- [ ] Update existing examples with logging patterns

## Documentation Requirements

### Developer Documentation
1. **Quick Start Guide**: How to enable logging in 5 minutes
2. **Template Customization**: Creating custom logging templates
3. **Integration Patterns**: Common logging integration scenarios
4. **Performance Guide**: Optimizing logging for production
5. **Troubleshooting**: Common issues and solutions

### Code Examples
1. **Basic Logging**: Simple start/end logging
2. **Structured Logging**: Rich contextual logging
3. **Performance Logging**: Timing and metrics
4. **Error Logging**: Comprehensive error handling
5. **Custom Templates**: Building your own logging templates

### API Documentation
1. **LoggingCodeTemplate API**: All public methods and properties
2. **Configuration Options**: Available logging settings
3. **Extension Points**: How to extend logging behavior
4. **Integration Interfaces**: Working with DI containers

## Testing Strategy

### Unit Tests
- [ ] Test logging template code generation
- [ ] Verify optional parameter injection
- [ ] Test performance with/without logging
- [ ] Validate backward compatibility

### Integration Tests
- [ ] Test with real database operations
- [ ] Verify logging output format
- [ ] Test with different logging frameworks
- [ ] Performance benchmarking

### End-to-End Tests
- [ ] Complete application scenarios
- [ ] Multiple logging configurations
- [ ] Error handling scenarios
- [ ] Production-like performance testing

## Risk Assessment

### High Risk
- **Performance Impact**: Adding logging could slow down generated methods
  - *Mitigation*: Ensure zero overhead when disabled, use conditional compilation
- **Breaking Changes**: New parameters might break existing code
  - *Mitigation*: Make all logging parameters optional with defaults

### Medium Risk
- **Complexity**: Adding logging increases template complexity
  - *Mitigation*: Keep logging optional, provide simple examples
- **Maintenance**: More code to maintain and test
  - *Mitigation*: Focus on simple, well-tested patterns

### Low Risk
- **Adoption**: Developers might not use logging features
  - *Mitigation*: Provide compelling examples and documentation

## Success Metrics

### Adoption Metrics
- Number of projects using logging templates
- GitHub issues/discussions about logging
- Documentation page views

### Performance Metrics
- Zero overhead when logging disabled (benchmark tests)
- Minimal overhead when logging enabled (<5% performance impact)
- Memory allocation impact measurement

### Quality Metrics
- Test coverage >90% for logging components
- Zero breaking changes to existing APIs
- Documentation completeness score

## Future Considerations

### OpenTelemetry Integration
- Add support for distributed tracing
- Integrate with OpenTelemetry metrics
- Provide correlation ID propagation

### Advanced Observability
- Query performance analysis
- Automatic slow query detection
- Database health monitoring

### Tooling Integration
- Visual Studio debugger integration
- Application Insights support
- Custom observability dashboards

## Conclusion

This PRD provides a comprehensive plan for adding optional logging and observability capabilities to the DataProvider project. The approach maintains the project's core principles of performance, type safety, and functional programming while providing developers with the tools they need for production observability.

The implementation will be incremental, starting with basic logging capabilities and expanding to more advanced features based on user feedback and adoption.
