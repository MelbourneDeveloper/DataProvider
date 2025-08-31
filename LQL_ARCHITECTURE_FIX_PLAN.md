# LQL Architecture Fix Plan

## Current Problem

The LQL transpiler is fundamentally broken because it was designed around `SqlStatement` (SELECT-only) but needs to support full procedural SQL with multiple statement types.

**Current broken architecture:**
```
LQL → SqlStatement (SELECT only) → SQL
```

**What we actually need:**
```
LQL → [SqlStatement, InsertStatement, UpdateStatement, DeleteStatement, VariableDeclaration] → Multiple SQL statements
```

## Core Issues

### 1. Single Statement Limitation
- `SqlStatement` is hardcoded for SELECT operations only
- `ISqlContext.GenerateSQL()` only produces SELECT statements
- INSERT, UPDATE, DELETE operations are ignored or broken

### 2. No Procedural Support
- LQL supports `let` bindings (variables) but transpiler ignores them
- No variable resolution or scoping
- No support for multiple statements in a single LQL program
- Complex expressions with variable references fail

### 3. Architectural Mismatch
- LQL is designed as a procedural language
- Current transpiler treats it as a single-expression language
- Missing intermediate representation for complex programs

## Proposed Solution

### Phase 1: Multiple Statement Types

#### 1.1 Create Statement Type Hierarchy
```csharp
// Base interface for all SQL statements
public interface ISqlStatement
{
    string GenerateSQL(SqlDialect dialect);
}

// Specific statement types
public class SelectStatement : ISqlStatement
public class InsertStatement : ISqlStatement  
public class UpdateStatement : ISqlStatement
public class DeleteStatement : ISqlStatement
public class VariableDeclaration : ISqlStatement
```

#### 1.2 Replace SqlStatement Usage
- Update `LqlStatementConverter` to return `ISqlStatement[]`
- Modify all SQL generation to use statement hierarchy
- Keep `SqlStatement` for backward compatibility but mark as legacy

#### 1.3 Update Pipeline Processing
```csharp
public static ISqlStatement[] ConvertPipelineToStatements(Pipeline pipeline)
{
    // Detect statement type from pipeline operations
    // Generate appropriate statement objects
    // Handle INSERT, UPDATE, DELETE wrapping
}
```

### Phase 2: Procedural Support

#### 2.1 Variable Resolution System
```csharp
public class LqlProgram
{
    public Dictionary<string, Pipeline> Variables { get; set; }  // let bindings
    public Pipeline MainExpression { get; set; }                 // final expression
}

public class VariableResolver
{
    public Pipeline ResolveVariables(Pipeline pipeline, Dictionary<string, Pipeline> scope)
    {
        // Resolve variable references in pipelines
        // Substitute variable names with their pipeline definitions  
        // Build complete pipeline with all operations expanded
    }
}
```

#### 2.2 Multi-Statement Programs
```csharp
public class LqlStatementConverter
{
    public static ISqlStatement[] ConvertProgram(string lqlCode)
    {
        var program = ParseLqlProgram(lqlCode);  // Parse let bindings + main expression
        var resolved = ResolveVariables(program); // Substitute variables
        return GenerateStatements(resolved);      // Generate SQL statements
    }
}
```

#### 2.3 Enhanced Parser
- Extend ANTLR grammar to parse `let` bindings
- Build AST with variable declarations
- Support complex expressions with variable references

### Phase 3: SQL Generation Overhaul

#### 3.1 Dialect-Specific Generation
```csharp
public interface ISqlDialectGenerator
{
    string GenerateSelect(SelectStatement stmt);
    string GenerateInsert(InsertStatement stmt);
    string GenerateUpdate(UpdateStatement stmt);
    string GenerateDelete(DeleteStatement stmt);
}

public class SqliteGenerator : ISqlDialectGenerator
public class PostgreSqlGenerator : ISqlDialectGenerator  
public class SqlServerGenerator : ISqlDialectGenerator
```

#### 3.2 Complex Query Support
- Handle UNION operations across variable boundaries
- Support subqueries from variable references
- Generate proper column lists for INSERT statements
- Handle complex JOINs with variable resolution

### Phase 4: Testing & Integration

#### 4.1 Test Infrastructure
```csharp
// Enhanced test cases for complex scenarios
public class LqlComplexTests
{
    [Theory]
    [InlineData("multiple_statements", "SQLite")]
    [InlineData("complex_unions", "PostgreSql")]
    [InlineData("variable_resolution", "SqlServer")]
    public void LqlProgram_ShouldGenerateCorrectSQL(string testCase, string dialect)
}
```

#### 4.2 Backward Compatibility
- Keep existing simple LQL working
- Gradual migration path
- Legacy support for current usage

## Implementation Priority

### High Priority (Fix Current Failures)
1. **Fix INSERT support** ✅ DONE
2. **Implement basic variable resolution** for `let` bindings
3. **Fix complex_join_union test** by resolving variable references

### Medium Priority (Architecture Improvements)  
4. Create statement type hierarchy
5. Update SQL generation to support multiple statement types
6. Enhance parser for procedural constructs

### Low Priority (Advanced Features)
7. Full procedural support (loops, conditionals)
8. Stored procedure generation
9. Advanced optimization

## File Changes Required

### Core Architecture
- `/Lql/Lql/ISqlStatement.cs` - New statement interface hierarchy
- `/Lql/Lql/LqlProgram.cs` - Program representation with variables
- `/Lql/Lql/VariableResolver.cs` - Variable resolution logic

### Parser Updates
- `/Lql/Lql/Parsing/LqlToAstVisitor.cs` - Enhanced AST building
- `/Lql/Lql/LqlStatementConverter.cs` - Multi-statement conversion

### SQL Generation
- `/Lql/Lql.SQLite/SqliteStatementGenerator.cs` - Dialect-specific generation
- `/Lql/Lql.Postgres/PostgreSqlStatementGenerator.cs`
- `/Lql/Lql.SqlServer/SqlServerStatementGenerator.cs`

### Testing
- `/Lql/Lql.Tests/LqlProgramTests.cs` - Test procedural features
- Update existing file-based tests to handle complex scenarios

## Success Criteria

1. ✅ `complex_join_union` test passes for all dialects
2. ✅ INSERT statements generate correctly  
3. ✅ Variable resolution works for `let` bindings
4. ✅ UNION operations work across variable boundaries
5. ✅ All existing tests continue to pass
6. ✅ Multiple statement types supported (SELECT, INSERT, UPDATE, DELETE)

## Timeline Estimate

- **Phase 1**: 2-3 days - Fix immediate failures, basic statement types
- **Phase 2**: 1-2 weeks - Full procedural support 
- **Phase 3**: 1 week - SQL generation overhaul
- **Phase 4**: 3-5 days - Testing and integration

**Total**: ~3-4 weeks for complete architectural fix

---

**Current Status**: INSERT functionality fixed ✅, working on variable resolution for procedural support.