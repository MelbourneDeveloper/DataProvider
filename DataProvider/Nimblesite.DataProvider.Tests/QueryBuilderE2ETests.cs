using System.Data;
using Microsoft.Data.Sqlite;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: SelectStatement LINQ builder -> ToSQLite -> execute against real DB.
/// Also tests GetRecords API and PredicateBuilder workflows.
/// </summary>
public sealed class QueryBuilderE2ETests : IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"qb_e2e_{Guid.NewGuid()}.db");

    private readonly SqliteConnection _connection;

    public QueryBuilderE2ETests()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();
        CreateSchemaAndSeed();
    }

    public void Dispose()
    {
        _connection.Dispose();
        try { File.Delete(_dbPath); }
        catch { /* cleanup best-effort */ }
    }

    private sealed record Product(string Id, string Name, double Price, int Quantity, string Category);

    private void CreateSchemaAndSeed()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Products (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                Quantity INTEGER NOT NULL,
                Category TEXT NOT NULL
            );
            CREATE TABLE Categories (
                Name TEXT PRIMARY KEY,
                Description TEXT NOT NULL
            );
            CREATE TABLE Suppliers (
                Id TEXT PRIMARY KEY,
                CompanyName TEXT NOT NULL,
                Country TEXT NOT NULL
            );
            CREATE TABLE ProductSuppliers (
                ProductId TEXT NOT NULL,
                SupplierId TEXT NOT NULL,
                PRIMARY KEY (ProductId, SupplierId)
            );
            """;
        cmd.ExecuteNonQuery();

        // Seed products
        string[] products =
        [
            "('p1', 'Widget A', 10.00, 100, 'Electronics')",
            "('p2', 'Widget B', 25.50, 50, 'Electronics')",
            "('p3', 'Gadget X', 99.99, 10, 'Gadgets')",
            "('p4', 'Gadget Y', 149.99, 5, 'Gadgets')",
            "('p5', 'Tool Alpha', 35.00, 75, 'Tools')",
            "('p6', 'Tool Beta', 45.00, 30, 'Tools')",
            "('p7', 'Tool Gamma', 15.00, 200, 'Tools')",
            "('p8', 'Premium Widget', 500.00, 2, 'Electronics')",
        ];
        foreach (var p in products)
        {
            using var insertCmd = _connection.CreateCommand();
            insertCmd.CommandText = $"INSERT INTO Products VALUES {p}";
            insertCmd.ExecuteNonQuery();
        }

        // Seed categories
        using var catCmd = _connection.CreateCommand();
        catCmd.CommandText = """
            INSERT INTO Categories VALUES ('Electronics', 'Electronic devices and components');
            INSERT INTO Categories VALUES ('Gadgets', 'Innovative gadgets');
            INSERT INTO Categories VALUES ('Tools', 'Professional tools');
            """;
        catCmd.ExecuteNonQuery();

        // Seed suppliers
        using var supCmd = _connection.CreateCommand();
        supCmd.CommandText = """
            INSERT INTO Suppliers VALUES ('s1', 'Acme Corp', 'US');
            INSERT INTO Suppliers VALUES ('s2', 'Global Parts', 'UK');
            INSERT INTO ProductSuppliers VALUES ('p1', 's1');
            INSERT INTO ProductSuppliers VALUES ('p1', 's2');
            INSERT INTO ProductSuppliers VALUES ('p3', 's1');
            """;
        supCmd.ExecuteNonQuery();
    }

    [Fact]
    public void SelectStatementBuilder_WhereOrderByLimit_ExecutesCorrectly()
    {
        // Build query: SELECT Name, Price FROM Products WHERE Category = 'Tools' ORDER BY Price ASC LIMIT 2
        var statement = "Products".From()
            .Select(columns: [(null, "Name"), (null, "Price")])
            .Where(columnName: "Category", value: "Tools")
            .OrderBy(columnName: "Price")
            .Take(count: 2)
            .ToSqlStatement();

        // Convert to SQLite
        var sqlResult = statement.ToSQLite();
        Assert.IsType<StringOk>(sqlResult);
        var sql = ((StringOk)sqlResult).Value;
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Products", sql);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);

        // Execute against real DB
        var queryResult = _connection.Query<(string Name, double Price)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );
        Assert.IsType<Result<IReadOnlyList<(string, double)>, SqlError>.Ok<IReadOnlyList<(string, double)>, SqlError>>(queryResult);
        var rows = ((Result<IReadOnlyList<(string, double)>, SqlError>.Ok<IReadOnlyList<(string, double)>, SqlError>)queryResult).Value;
        Assert.Equal(2, rows.Count);
        Assert.Equal("Tool Gamma", rows[0].Name);
        Assert.Equal(15.00, rows[0].Price);
        Assert.Equal("Tool Alpha", rows[1].Name);
        Assert.Equal(35.00, rows[1].Price);
    }

    [Fact]
    public void SelectStatementBuilder_DistinctAndGroupBy_ExecutesCorrectly()
    {
        // DISTINCT categories
        var distinctStmt = "Products".From()
            .Select(columns: [(null, "Category")])
            .Distinct()
            .OrderBy(columnName: "Category")
            .ToSqlStatement();

        var distinctSql = ((StringOk)distinctStmt.ToSQLite()).Value;
        var distinctResult = _connection.Query<string>(
            sql: distinctSql,
            mapper: r => r.GetString(0)
        );
        var categories = ((Result<IReadOnlyList<string>, SqlError>.Ok<IReadOnlyList<string>, SqlError>)distinctResult).Value;
        Assert.Equal(3, categories.Count);
        Assert.Equal("Electronics", categories[0]);
        Assert.Equal("Gadgets", categories[1]);
        Assert.Equal("Tools", categories[2]);
    }

    [Fact]
    public void SelectStatementBuilder_WithPagination_ExecutesCorrectly()
    {
        // Page 2 (skip 3, take 3) ordered by name
        var pagedStmt = "Products".From()
            .SelectAll()
            .OrderBy(columnName: "Name")
            .Skip(count: 3)
            .Take(count: 3)
            .ToSqlStatement();

        var pagedSql = ((StringOk)pagedStmt.ToSQLite()).Value;
        Assert.Contains("OFFSET", pagedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", pagedSql, StringComparison.OrdinalIgnoreCase);

        var pagedResult = _connection.Query<string>(
            sql: pagedSql,
            mapper: r => r.GetString(1) // Name is column index 1
        );
        var page = ((Result<IReadOnlyList<string>, SqlError>.Ok<IReadOnlyList<string>, SqlError>)pagedResult).Value;
        Assert.Equal(3, page.Count);
    }

    [Fact]
    public void SelectStatementBuilder_Join_ExecutesCorrectly()
    {
        // JOIN Products with Categories
        var joinStmt = "Products".From()
            .Select(columns: [("Products", "Name"), ("Categories", "Description")])
            .InnerJoin(
                rightTable: "Categories",
                leftColumn: "Category",
                rightColumn: "Name",
                leftTableAlias: "Products",
                rightTableAlias: "Categories"
            )
            .Where(columnName: "Category", value: "Electronics")
            .OrderBy(columnName: "Products.Name")
            .ToSqlStatement();

        var joinSql = ((StringOk)joinStmt.ToSQLite()).Value;
        Assert.Contains("JOIN", joinSql, StringComparison.OrdinalIgnoreCase);

        var joinResult = _connection.Query<(string ProductName, string CatDesc)>(
            sql: joinSql,
            mapper: r => (r.GetString(0), r.GetString(1))
        );
        Assert.IsType<Result<IReadOnlyList<(string, string)>, SqlError>.Ok<IReadOnlyList<(string, string)>, SqlError>>(joinResult);
        var joined = ((Result<IReadOnlyList<(string, string)>, SqlError>.Ok<IReadOnlyList<(string, string)>, SqlError>)joinResult).Value;
        Assert.Equal(3, joined.Count);
        Assert.All(joined, j => Assert.Equal("Electronic devices and components", j.CatDesc));
    }

    [Fact]
    public void GetRecords_WithSelectStatementAndSQLiteGenerator_MapsResultsCorrectly()
    {
        // Build a SelectStatement
        var statement = "Products".From()
            .Select(columns: [(null, "Id"), (null, "Name"), (null, "Price")])
            .Where(columnName: "Price", ComparisonOperator.GreaterThan, 40.0)
            .OrderBy(columnName: "Price")
            .ToSqlStatement();

        // Use GetRecords with the SQLite generator
        var result = _connection.GetRecords(
            statement: statement,
            sqlGenerator: stmt => stmt.ToSQLite(),
            mapper: r => new { Id = r.GetString(0), Name = r.GetString(1), Price = r.GetDouble(2) }
        );

        Assert.IsType<Result<IReadOnlyList<dynamic>, SqlError>.Ok<IReadOnlyList<dynamic>, SqlError>>(result);
        var records = ((Result<IReadOnlyList<dynamic>, SqlError>.Ok<IReadOnlyList<dynamic>, SqlError>)result).Value;
        Assert.Equal(4, records.Count);
        Assert.Equal("Tool Beta", (string)records[0].Name);
        Assert.Equal(45.00, (double)records[0].Price);
        Assert.Equal("Premium Widget", (string)records[3].Name);
        Assert.Equal(500.00, (double)records[3].Price);
    }

    [Fact]
    public void GetRecords_NullGuards_ReturnErrors()
    {
        var statement = "Products".From().SelectAll().ToSqlStatement();

        // Null connection
        var nullConn = DbConnectionExtensions.GetRecords<string>(
            connection: null!,
            statement: statement,
            sqlGenerator: s => s.ToSQLite(),
            mapper: r => r.GetString(0)
        );
        Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>>(nullConn);

        // Null statement
        var nullStmt = _connection.GetRecords<string>(
            statement: null!,
            sqlGenerator: s => s.ToSQLite(),
            mapper: r => r.GetString(0)
        );
        Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>>(nullStmt);

        // Null generator
        var nullGen = _connection.GetRecords<string>(
            statement: statement,
            sqlGenerator: null!,
            mapper: r => r.GetString(0)
        );
        Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>>(nullGen);

        // Null mapper
        var nullMapper = _connection.GetRecords<string>(
            statement: statement,
            sqlGenerator: s => s.ToSQLite(),
            mapper: null!
        );
        Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>>(nullMapper);
    }

    [Fact]
    public void SelectStatementToSQLite_VariousStatements_GeneratesValidSQL()
    {
        // Simple select all
        var simple = "Products".From().SelectAll().ToSqlStatement();
        var simpleSql = ((StringOk)simple.ToSQLite()).Value;
        Assert.Contains("SELECT *", simpleSql);
        Assert.Contains("FROM Products", simpleSql);

        // With specific columns
        var cols = "Products".From()
            .Select(columns: [(null, "Name"), (null, "Price")])
            .ToSqlStatement();
        var colsSql = ((StringOk)cols.ToSQLite()).Value;
        Assert.Contains("Name", colsSql);
        Assert.Contains("Price", colsSql);
        Assert.DoesNotContain("*", colsSql);

        // With WHERE + AND
        var filtered = "Products".From()
            .SelectAll()
            .Where(columnName: "Category", value: "Tools")
            .And(columnName: "Price", value: 35.0)
            .ToSqlStatement();
        var filteredSql = ((StringOk)filtered.ToSQLite()).Value;
        Assert.Contains("WHERE", filteredSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AND", filteredSql, StringComparison.OrdinalIgnoreCase);

        // Verify each generated SQL actually executes
        foreach (var sql in new[] { simpleSql, colsSql, filteredSql })
        {
            var result = _connection.Query<string>(sql: sql, mapper: r => r.GetString(0));
            Assert.IsNotType<Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>>(result);
        }
    }

    [Fact]
    public void SelectStatementBuilder_MultipleWhereConditions_GeneratesCorrectResults()
    {
        // OR condition: Electronics or Gadgets
        var orStmt = "Products".From()
            .Select(columns: [(null, "Name"), (null, "Category")])
            .Where(columnName: "Category", value: "Electronics")
            .Or(columnName: "Category", value: "Gadgets")
            .OrderBy(columnName: "Name")
            .ToSqlStatement();

        var orSql = ((StringOk)orStmt.ToSQLite()).Value;
        Assert.Contains("OR", orSql, StringComparison.OrdinalIgnoreCase);

        var orResult = _connection.Query<(string Name, string Category)>(
            sql: orSql,
            mapper: r => (r.GetString(0), r.GetString(1))
        );
        var orRows = ((Result<IReadOnlyList<(string, string)>, SqlError>.Ok<IReadOnlyList<(string, string)>, SqlError>)orResult).Value;
        Assert.Equal(5, orRows.Count);
        Assert.All(orRows, r => Assert.True(r.Category is "Electronics" or "Gadgets"));
    }

    [Fact]
    public void SelectStatementBuilder_ExpressionColumns_GeneratesCorrectSQL()
    {
        // Use expression column for computed values
        var builder = new SelectStatementBuilder();
        builder.AddTable(name: "Products");
        builder.AddSelectColumn(name: "Name");
        builder.AddSelectColumn(ColumnInfo.FromExpression(
            expression: "Price * Quantity",
            alias: "TotalValue"
        ));
        builder.AddOrderBy(column: "Name", direction: "ASC");
        var stmt = builder.Build();

        var sql = ((StringOk)stmt.ToSQLite()).Value;
        Assert.Contains("Price * Quantity", sql);
        Assert.Contains("TotalValue", sql);

        var result = _connection.Query<(string Name, double TotalValue)>(
            sql: sql,
            mapper: r => (r.GetString(0), r.GetDouble(1))
        );
        var rows = ((Result<IReadOnlyList<(string, double)>, SqlError>.Ok<IReadOnlyList<(string, double)>, SqlError>)result).Value;
        Assert.Equal(8, rows.Count);

        // Verify computed values
        var gadgetX = rows.First(r => r.Name == "Gadget X");
        Assert.Equal(999.90, gadgetX.TotalValue, precision: 2);

        var premiumWidget = rows.First(r => r.Name == "Premium Widget");
        Assert.Equal(1000.00, premiumWidget.TotalValue, precision: 2);
    }
}
