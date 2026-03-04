using System.Collections.Immutable;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Migration;
using Migration.SQLite;
using Outcome;
using Reporting.Engine;
using Selecta;
using Xunit;
using ConnError = Outcome.Result<System.Data.IDbConnection, Selecta.SqlError>.Error<
    System.Data.IDbConnection,
    Selecta.SqlError
>;
using ConnOk = Outcome.Result<System.Data.IDbConnection, Selecta.SqlError>.Ok<
    System.Data.IDbConnection,
    Selecta.SqlError
>;
using ConnResult = Outcome.Result<System.Data.IDbConnection, Selecta.SqlError>;
using EngineError = Outcome.Result<Reporting.Engine.ReportExecutionResult, Selecta.SqlError>.Error<
    Reporting.Engine.ReportExecutionResult,
    Selecta.SqlError
>;
using EngineOk = Outcome.Result<Reporting.Engine.ReportExecutionResult, Selecta.SqlError>.Ok<
    Reporting.Engine.ReportExecutionResult,
    Selecta.SqlError
>;

namespace Reporting.Tests;

#pragma warning disable CS1591

public sealed class ReportEngineTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public ReportEngineTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"reporting_test_{Guid.NewGuid()}.db");
        _connectionString = $"Data Source={_dbPath}";
        _connection = new SqliteConnection(_connectionString);
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<ReportEngineTests>();

        SetupDatabase();
    }

    [Fact]
    public void Execute_WithSqlDataSource_ReturnsCorrectData()
    {
        // Arrange
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Sql,
            query: "SELECT Id, Name, Category, Price, Stock FROM products ORDER BY Name"
        );

        var parameters = ImmutableDictionary<string, string>.Empty;

        // Act
        var result = ReportEngine.Execute(
            report: report,
            parameters: parameters,
            connectionFactory: CreateTestConnection,
            lqlTranspiler: TranspileLql,
            logger: _logger
        );

        // Assert
        Assert.True(result is EngineOk, $"Expected success but got {result.GetType()}");

        var ok = (EngineOk)result;
        Assert.Equal("test-report", ok.Value.ReportId);
        Assert.True(ok.Value.DataSources.ContainsKey("testDs"));

        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Equal(5, dsResult.ColumnNames.Length);
        Assert.Contains("Id", dsResult.ColumnNames);
        Assert.Contains("Name", dsResult.ColumnNames);
        Assert.Equal(4, dsResult.TotalRows);
    }

    [Fact]
    public void Execute_WithGroupByQuery_ReturnsAggregatedData()
    {
        // Arrange
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Sql,
            query: "SELECT Category, COUNT(*) as ProductCount FROM products GROUP BY Category ORDER BY ProductCount DESC"
        );

        // Act
        var result = ReportEngine.Execute(
            report: report,
            parameters: ImmutableDictionary<string, string>.Empty,
            connectionFactory: CreateTestConnection,
            lqlTranspiler: TranspileLql,
            logger: _logger
        );

        // Assert
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Equal(2, dsResult.ColumnNames.Length);
        Assert.Contains("Category", dsResult.ColumnNames);
        Assert.Contains("ProductCount", dsResult.ColumnNames);
        Assert.True(dsResult.TotalRows >= 2);
    }

    [Fact]
    public void Execute_WithMissingConnection_ReturnsError()
    {
        // Arrange
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Sql,
            query: "SELECT * FROM products",
            connectionRef: "nonexistent-db"
        );

        // Act
        var result = ReportEngine.Execute(
            report: report,
            parameters: ImmutableDictionary<string, string>.Empty,
            connectionFactory: CreateTestConnection,
            lqlTranspiler: TranspileLql,
            logger: _logger
        );

        // Assert
        Assert.True(result is EngineError, $"Expected error but got {result.GetType()}");
        var err = (EngineError)result;
        Assert.Contains("not found", err.Value.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_WithInvalidSql_ReturnsError()
    {
        // Arrange
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Sql,
            query: "SELECT * FROM nonexistent_table"
        );

        // Act
        var result = ReportEngine.Execute(
            report: report,
            parameters: ImmutableDictionary<string, string>.Empty,
            connectionFactory: CreateTestConnection,
            lqlTranspiler: TranspileLql,
            logger: _logger
        );

        // Assert
        Assert.True(result is EngineError);
    }

    [Fact]
    public void Execute_WithMultipleDataSources_ReturnsAllResults()
    {
        // Arrange
        var report = new ReportDefinition(
            Id: "multi-ds-report",
            Title: "Multi Data Source Report",
            Parameters: [],
            DataSources:
            [
                new DataSourceDefinition(
                    Id: "products",
                    Type: DataSourceType.Sql,
                    ConnectionRef: "test-db",
                    Query: "SELECT Name, Price FROM products ORDER BY Name",
                    Url: null,
                    Method: null,
                    Headers: null,
                    Parameters: []
                ),
                new DataSourceDefinition(
                    Id: "summary",
                    Type: DataSourceType.Sql,
                    ConnectionRef: "test-db",
                    Query: "SELECT COUNT(*) as Total FROM products",
                    Url: null,
                    Method: null,
                    Headers: null,
                    Parameters: []
                ),
            ],
            Layout: new LayoutDefinition(Columns: 12, Rows: [])
        );

        // Act
        var result = ReportEngine.Execute(
            report: report,
            parameters: ImmutableDictionary<string, string>.Empty,
            connectionFactory: CreateTestConnection,
            lqlTranspiler: TranspileLql,
            logger: _logger
        );

        // Assert
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        Assert.Equal(2, ok.Value.DataSources.Count);
        Assert.True(ok.Value.DataSources.ContainsKey("products"));
        Assert.True(ok.Value.DataSources.ContainsKey("summary"));

        var summaryResult = ok.Value.DataSources["summary"];
        Assert.Single(summaryResult.Rows);
    }

    [Fact]
    public void Execute_WithEmptyTable_ReturnsEmptyResult()
    {
        // Arrange - query a table that exists but is empty (we'll use a WHERE that matches nothing)
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Sql,
            query: "SELECT * FROM products WHERE Name = 'NonExistentProduct'"
        );

        // Act
        var result = ReportEngine.Execute(
            report: report,
            parameters: ImmutableDictionary<string, string>.Empty,
            connectionFactory: CreateTestConnection,
            lqlTranspiler: TranspileLql,
            logger: _logger
        );

        // Assert
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        Assert.Equal(0, ok.Value.DataSources["testDs"].TotalRows);
    }

    [Fact]
    public void Execute_WithLqlSelect_TranspilesAndReturnsData()
    {
        // Arrange - LQL pipeline: select columns with ordering
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Lql,
            query: "products |> order_by(products.Name asc) |> select(products.Id, products.Name, products.Category, products.Price, products.Stock)"
        );

        // Act
        var result = ReportEngine.Execute(
            report: report,
            parameters: ImmutableDictionary<string, string>.Empty,
            connectionFactory: CreateTestConnection,
            lqlTranspiler: TranspileLql,
            logger: _logger
        );

        // Assert
        Assert.True(result is EngineOk, $"Expected success but got {result.GetType()}");
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Equal(5, dsResult.ColumnNames.Length);
        Assert.Equal(4, dsResult.TotalRows);
    }

    [Fact]
    public void Execute_WithLqlGroupByAggregation_ReturnsAggregatedData()
    {
        // Arrange - LQL pipeline: group_by + count + sum + avg
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Lql,
            query: "products |> group_by(products.Category) |> select(products.Category, count(*) as ProductCount, sum(products.Stock) as TotalStock, avg(products.Price) as AvgPrice) |> order_by(ProductCount desc)"
        );

        // Act
        var result = ReportEngine.Execute(
            report: report,
            parameters: ImmutableDictionary<string, string>.Empty,
            connectionFactory: CreateTestConnection,
            lqlTranspiler: TranspileLql,
            logger: _logger
        );

        // Assert
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Contains("Category", dsResult.ColumnNames);
        Assert.Contains("ProductCount", dsResult.ColumnNames);
        Assert.Contains("TotalStock", dsResult.ColumnNames);
        Assert.Contains("AvgPrice", dsResult.ColumnNames);
        Assert.Equal(2, dsResult.TotalRows);
    }

    [Fact]
    public void Execute_WithLqlFilterAndArithmetic_ReturnsFilteredData()
    {
        // Arrange - LQL pipeline: filter + arithmetic expression
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Lql,
            query: "products |> filter(fn(row) => row.products.Price > 30) |> select(products.Name, products.Price, products.Stock, products.Price * products.Stock as InventoryValue) |> order_by(InventoryValue desc)"
        );

        // Act
        var result = ReportEngine.Execute(
            report: report,
            parameters: ImmutableDictionary<string, string>.Empty,
            connectionFactory: CreateTestConnection,
            lqlTranspiler: TranspileLql,
            logger: _logger
        );

        // Assert - only products with Price > 30: Beta Gadget (49.99), Delta Gadget (79.99)
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Contains("InventoryValue", dsResult.ColumnNames);
        Assert.Equal(2, dsResult.TotalRows);
    }

    [Fact]
    public void Execute_WithLqlCaseExpression_ReturnsDerivedColumn()
    {
        // Arrange - LQL pipeline: CASE expression for price tiers
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Lql,
            query: "products |> select(products.Name, products.Price, case when products.Price > 50 then 'Premium' when products.Price > 25 then 'Standard' else 'Budget' end as PriceTier) |> order_by(products.Price desc)"
        );

        // Act
        var result = ReportEngine.Execute(
            report: report,
            parameters: ImmutableDictionary<string, string>.Empty,
            connectionFactory: CreateTestConnection,
            lqlTranspiler: TranspileLql,
            logger: _logger
        );

        // Assert
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Contains("PriceTier", dsResult.ColumnNames);
        Assert.Equal(4, dsResult.TotalRows);
    }

    [Fact]
    public void Execute_WithLqlGroupByHaving_FiltersAggregates()
    {
        // Arrange - LQL pipeline: group_by + having + min/max
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Lql,
            query: "products |> group_by(products.Category) |> having(fn(group) => count(*) > 1) |> select(products.Category, count(*) as Items, sum(products.Stock) as TotalStock, min(products.Price) as CheapestPrice, max(products.Price) as MostExpensive) |> order_by(TotalStock desc)"
        );

        // Act
        var result = ReportEngine.Execute(
            report: report,
            parameters: ImmutableDictionary<string, string>.Empty,
            connectionFactory: CreateTestConnection,
            lqlTranspiler: TranspileLql,
            logger: _logger
        );

        // Assert - both categories (Widgets: 2, Gadgets: 2) pass having(count > 1)
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Contains("CheapestPrice", dsResult.ColumnNames);
        Assert.Contains("MostExpensive", dsResult.ColumnNames);
        Assert.Equal(2, dsResult.TotalRows);
    }

    [Fact]
    public void Execute_WithLqlAggregateNoGroupBy_ReturnsSingleRow()
    {
        // Arrange - LQL pipeline: aggregate functions without group_by (totals)
        var report = CreateTestReport(
            dataSourceType: DataSourceType.Lql,
            query: "products |> select(count(*) as TotalProducts, sum(products.Stock) as TotalStock, sum(products.Price * products.Stock) as TotalValue)"
        );

        // Act
        var result = ReportEngine.Execute(
            report: report,
            parameters: ImmutableDictionary<string, string>.Empty,
            connectionFactory: CreateTestConnection,
            lqlTranspiler: TranspileLql,
            logger: _logger
        );

        // Assert
        Assert.True(result is EngineOk);
        var ok = (EngineOk)result;
        var dsResult = ok.Value.DataSources["testDs"];
        Assert.Contains("TotalProducts", dsResult.ColumnNames);
        Assert.Contains("TotalStock", dsResult.ColumnNames);
        Assert.Contains("TotalValue", dsResult.ColumnNames);
        Assert.Equal(1, dsResult.TotalRows);
    }

    private static ReportDefinition CreateTestReport(
        DataSourceType dataSourceType,
        string query,
        string connectionRef = "test-db"
    ) =>
        new(
            Id: "test-report",
            Title: "Test Report",
            Parameters: [],
            DataSources:
            [
                new DataSourceDefinition(
                    Id: "testDs",
                    Type: dataSourceType,
                    ConnectionRef: connectionRef,
                    Query: query,
                    Url: null,
                    Method: null,
                    Headers: null,
                    Parameters: []
                ),
            ],
            Layout: new LayoutDefinition(Columns: 12, Rows: [])
        );

    private ConnResult CreateTestConnection(string connectionRef)
    {
        if (connectionRef != "test-db")
        {
            return new ConnError(SqlError.Create($"Connection '{connectionRef}' not found"));
        }

        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return new ConnOk(conn);
    }

    private static Result<string, SqlError> TranspileLql(string lqlCode)
    {
        var statementResult = Lql.LqlStatementConverter.ToStatement(lqlCode);
        if (
            statementResult
            is Result<Lql.LqlStatement, SqlError>.Error<Lql.LqlStatement, SqlError> stmtErr
        )
        {
            return new Result<string, SqlError>.Error<string, SqlError>(stmtErr.Value);
        }

        var statement = (
            (Result<Lql.LqlStatement, SqlError>.Ok<Lql.LqlStatement, SqlError>)statementResult
        ).Value;
        return Lql.SQLite.SqlStatementExtensionsSQLite.ToSQLite(statement);
    }

    private void SetupDatabase()
    {
        // Use Migration library with YAML schema to create the database
        var schemaYaml = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "reporting-test-schema.yaml")
        );
        var schema = SchemaYamlSerializer.FromYaml(schemaYaml);

        _connection.Open();

        // Generate DDL from YAML schema and execute
        foreach (var table in schema.Tables)
        {
            var createOp = new CreateTableOperation(table);
            var ddl = SqliteDdlGenerator.Generate(createOp);
            using var cmd = new SqliteCommand(ddl, _connection);
            cmd.ExecuteNonQuery();
        }

        // Insert test data using parameterized inserts
        InsertProduct(
            id: "prod-1",
            name: "Alpha Widget",
            category: "Widgets",
            price: 29.99,
            stock: 100
        );
        InsertProduct(
            id: "prod-2",
            name: "Beta Gadget",
            category: "Gadgets",
            price: 49.99,
            stock: 50
        );
        InsertProduct(
            id: "prod-3",
            name: "Gamma Widget",
            category: "Widgets",
            price: 19.99,
            stock: 200
        );
        InsertProduct(
            id: "prod-4",
            name: "Delta Gadget",
            category: "Gadgets",
            price: 79.99,
            stock: 25
        );
    }

    private void InsertProduct(string id, string name, string category, double price, int stock)
    {
        using var cmd = new SqliteCommand(
            "INSERT INTO products (Id, Name, Category, Price, Stock) VALUES (@id, @name, @category, @price, @stock)",
            _connection
        );
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@category", category);
        cmd.Parameters.AddWithValue("@price", price);
        cmd.Parameters.AddWithValue("@stock", stock);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _loggerFactory?.Dispose();
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            { /* File may be locked */
            }
        }
    }
}
