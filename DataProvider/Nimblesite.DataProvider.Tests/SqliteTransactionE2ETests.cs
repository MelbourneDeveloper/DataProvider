using System.Data;
using Microsoft.Data.Sqlite;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: transaction workflows with real SQLite databases.
/// Tests commit, rollback, multi-table transactions, and DbTransact helpers.
/// </summary>
public sealed class SqliteTransactionE2ETests : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"tx_e2e_{Guid.NewGuid()}.db"
    );

    private readonly SqliteConnection _connection;

    public SqliteTransactionE2ETests()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();
        CreateSchema();
    }

    public void Dispose()
    {
        _connection.Dispose();
        try
        {
            File.Delete(_dbPath);
        }
        catch
        { /* cleanup best-effort */
        }
    }

    private void CreateSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Orders (
                Id TEXT PRIMARY KEY,
                CustomerId TEXT NOT NULL,
                Total REAL NOT NULL,
                Status TEXT NOT NULL DEFAULT 'pending'
            );
            CREATE TABLE OrderItems (
                Id TEXT PRIMARY KEY,
                OrderId TEXT NOT NULL,
                ProductName TEXT NOT NULL,
                Quantity INTEGER NOT NULL,
                UnitPrice REAL NOT NULL,
                FOREIGN KEY (OrderId) REFERENCES Orders(Id)
            );
            CREATE TABLE Inventory (
                ProductName TEXT PRIMARY KEY,
                StockCount INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        // Seed inventory
        using var seedCmd = _connection.CreateCommand();
        seedCmd.CommandText = """
            INSERT INTO Inventory VALUES ('Widget', 100);
            INSERT INTO Inventory VALUES ('Gadget', 50);
            INSERT INTO Inventory VALUES ('Doohickey', 25);
            """;
        seedCmd.ExecuteNonQuery();
    }

    [Fact]
    public void TransactionCommit_MultiTableInsert_AllDataPersisted()
    {
        using var tx = _connection.BeginTransaction();

        // Insert order within transaction
        var orderId = Guid.NewGuid().ToString();
        var orderInsert = tx.Execute(
            sql: "INSERT INTO Orders (Id, CustomerId, Total, Status) VALUES (@id, @cid, @total, @status)",
            parameters:
            [
                new SqliteParameter("@id", orderId),
                new SqliteParameter("@cid", "CUST-001"),
                new SqliteParameter("@total", 150.75),
                new SqliteParameter("@status", "confirmed"),
            ]
        );
        Assert.IsType<IntOk>(orderInsert);
        Assert.Equal(1, ((IntOk)orderInsert).Value);

        // Insert order items within same transaction
        var item1Id = Guid.NewGuid().ToString();
        var item2Id = Guid.NewGuid().ToString();
        tx.Execute(
            sql: "INSERT INTO OrderItems (Id, OrderId, ProductName, Quantity, UnitPrice) VALUES (@id, @oid, @name, @qty, @price)",
            parameters:
            [
                new SqliteParameter("@id", item1Id),
                new SqliteParameter("@oid", orderId),
                new SqliteParameter("@name", "Widget"),
                new SqliteParameter("@qty", 3),
                new SqliteParameter("@price", 25.25),
            ]
        );
        tx.Execute(
            sql: "INSERT INTO OrderItems (Id, OrderId, ProductName, Quantity, UnitPrice) VALUES (@id, @oid, @name, @qty, @price)",
            parameters:
            [
                new SqliteParameter("@id", item2Id),
                new SqliteParameter("@oid", orderId),
                new SqliteParameter("@name", "Gadget"),
                new SqliteParameter("@qty", 2),
                new SqliteParameter("@price", 37.50),
            ]
        );

        // Update inventory within transaction
        tx.Execute(
            sql: "UPDATE Inventory SET StockCount = StockCount - @qty WHERE ProductName = @name",
            parameters: [new SqliteParameter("@qty", 3), new SqliteParameter("@name", "Widget")]
        );
        tx.Execute(
            sql: "UPDATE Inventory SET StockCount = StockCount - @qty WHERE ProductName = @name",
            parameters: [new SqliteParameter("@qty", 2), new SqliteParameter("@name", "Gadget")]
        );

        // Query within transaction to verify
        var itemCount = tx.Scalar<long>(
            sql: "SELECT COUNT(*) FROM OrderItems WHERE OrderId = @oid",
            parameters: [new SqliteParameter("@oid", orderId)]
        );
        Assert.Equal(2L, ((Result<long?, SqlError>.Ok<long?, SqlError>)itemCount).Value);

        // Commit
        tx.Commit();

        // Verify data persisted after commit
        var orderCheck = _connection.Scalar<long>(sql: "SELECT COUNT(*) FROM Orders");
        Assert.Equal(1L, ((Result<long?, SqlError>.Ok<long?, SqlError>)orderCheck).Value);

        var itemCheck = _connection.Scalar<long>(sql: "SELECT COUNT(*) FROM OrderItems");
        Assert.Equal(2L, ((Result<long?, SqlError>.Ok<long?, SqlError>)itemCheck).Value);

        var widgetStock = _connection.Scalar<long>(
            sql: "SELECT StockCount FROM Inventory WHERE ProductName = 'Widget'"
        );
        Assert.Equal(97L, ((Result<long?, SqlError>.Ok<long?, SqlError>)widgetStock).Value);

        var gadgetStock = _connection.Scalar<long>(
            sql: "SELECT StockCount FROM Inventory WHERE ProductName = 'Gadget'"
        );
        Assert.Equal(48L, ((Result<long?, SqlError>.Ok<long?, SqlError>)gadgetStock).Value);
    }

    [Fact]
    public void TransactionRollback_FailedOperation_NoDataPersisted()
    {
        // Verify initial state
        var initialOrders = _connection.Scalar<long>(sql: "SELECT COUNT(*) FROM Orders");
        Assert.Equal(0L, ((Result<long?, SqlError>.Ok<long?, SqlError>)initialOrders).Value);

        using var tx = _connection.BeginTransaction();

        // Insert order
        var orderId = Guid.NewGuid().ToString();
        tx.Execute(
            sql: "INSERT INTO Orders (Id, CustomerId, Total) VALUES (@id, @cid, @total)",
            parameters:
            [
                new SqliteParameter("@id", orderId),
                new SqliteParameter("@cid", "CUST-002"),
                new SqliteParameter("@total", 99.99),
            ]
        );

        // Insert items
        tx.Execute(
            sql: "INSERT INTO OrderItems (Id, OrderId, ProductName, Quantity, UnitPrice) VALUES (@id, @oid, @name, @qty, @price)",
            parameters:
            [
                new SqliteParameter("@id", Guid.NewGuid().ToString()),
                new SqliteParameter("@oid", orderId),
                new SqliteParameter("@name", "Widget"),
                new SqliteParameter("@qty", 5),
                new SqliteParameter("@price", 19.99),
            ]
        );

        // Verify data exists within transaction
        var txCount = tx.Scalar<long>(sql: "SELECT COUNT(*) FROM Orders");
        Assert.Equal(1L, ((Result<long?, SqlError>.Ok<long?, SqlError>)txCount).Value);

        // Rollback
        tx.Rollback();

        // Verify NO data persisted
        var afterRollback = _connection.Scalar<long>(sql: "SELECT COUNT(*) FROM Orders");
        Assert.Equal(0L, ((Result<long?, SqlError>.Ok<long?, SqlError>)afterRollback).Value);

        var itemsAfterRollback = _connection.Scalar<long>(sql: "SELECT COUNT(*) FROM OrderItems");
        Assert.Equal(0L, ((Result<long?, SqlError>.Ok<long?, SqlError>)itemsAfterRollback).Value);

        // Verify inventory unchanged
        var widgetStock = _connection.Scalar<long>(
            sql: "SELECT StockCount FROM Inventory WHERE ProductName = 'Widget'"
        );
        Assert.Equal(100L, ((Result<long?, SqlError>.Ok<long?, SqlError>)widgetStock).Value);
    }

    [Fact]
    public void TransactionQueryWorkflow_QueryAndModifyInTransaction_ConsistentReads()
    {
        // Seed some orders first (outside transaction)
        for (int i = 0; i < 5; i++)
        {
            _connection.Execute(
                sql: "INSERT INTO Orders (Id, CustomerId, Total, Status) VALUES (@id, @cid, @total, @status)",
                parameters:
                [
                    new SqliteParameter("@id", Guid.NewGuid().ToString()),
                    new SqliteParameter("@cid", $"CUST-{i:D3}"),
                    new SqliteParameter("@total", (i + 1) * 50.0),
                    new SqliteParameter("@status", i < 3 ? "pending" : "shipped"),
                ]
            );
        }

        using var tx = _connection.BeginTransaction();

        // Read pending orders in transaction
        var pendingOrders = tx.Query<(string Id, string CustomerId, double Total)>(
            sql: "SELECT Id, CustomerId, Total FROM Orders WHERE Status = @status ORDER BY Total",
            parameters: [new SqliteParameter("@status", "pending")],
            mapper: r => (r.GetString(0), r.GetString(1), r.GetDouble(2))
        );
        Assert.IsType<Result<IReadOnlyList<(string, string, double)>, SqlError>.Ok<
            IReadOnlyList<(string, string, double)>,
            SqlError
        >>(pendingOrders);
        var pending = (
            (Result<IReadOnlyList<(string, string, double)>, SqlError>.Ok<
                IReadOnlyList<(string, string, double)>,
                SqlError
            >)pendingOrders
        ).Value;
        Assert.Equal(3, pending.Count);
        Assert.Equal(50.0, pending[0].Total);
        Assert.Equal(100.0, pending[1].Total);
        Assert.Equal(150.0, pending[2].Total);

        // Update all pending to confirmed
        var updateResult = tx.Execute(
            sql: "UPDATE Orders SET Status = 'confirmed' WHERE Status = @status",
            parameters: [new SqliteParameter("@status", "pending")]
        );
        Assert.Equal(3, ((IntOk)updateResult).Value);

        // Verify within transaction
        var confirmedCount = tx.Scalar<long>(
            sql: "SELECT COUNT(*) FROM Orders WHERE Status = 'confirmed'"
        );
        Assert.Equal(3L, ((Result<long?, SqlError>.Ok<long?, SqlError>)confirmedCount).Value);

        // Aggregate within transaction
        var totalValue = tx.Scalar<double>(
            sql: "SELECT SUM(Total) FROM Orders WHERE Status = 'confirmed'"
        );
        Assert.Equal(300.0, ((Result<double?, SqlError>.Ok<double?, SqlError>)totalValue).Value);

        tx.Commit();

        // Verify after commit
        var finalPending = _connection.Scalar<long>(
            sql: "SELECT COUNT(*) FROM Orders WHERE Status = 'pending'"
        );
        Assert.Equal(0L, ((Result<long?, SqlError>.Ok<long?, SqlError>)finalPending).Value);
    }

    [Fact]
    public async Task DbTransactHelper_CommitAndRollbackWorkflows_WorkCorrectly()
    {
        // Use DbTransact helper for successful commit
        await _connection.Transact(async tx =>
        {
            var cmd = ((SqliteTransaction)tx).Connection!.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText =
                "INSERT INTO Orders (Id, CustomerId, Total) VALUES (@id, @cid, @total)";
            cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@cid", "CUST-TX-001");
            cmd.Parameters.AddWithValue("@total", 200.0);
            await cmd.ExecuteNonQueryAsync();
        });

        // Verify committed
        var count = _connection.Scalar<long>(
            sql: "SELECT COUNT(*) FROM Orders WHERE CustomerId = 'CUST-TX-001'"
        );
        Assert.Equal(1L, ((Result<long?, SqlError>.Ok<long?, SqlError>)count).Value);

        // Use DbTransact with return value
        var result = await _connection.Transact(async tx =>
        {
            var cmd = ((SqliteTransaction)tx).Connection!.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = "SELECT Total FROM Orders WHERE CustomerId = 'CUST-TX-001'";
            var value = await cmd.ExecuteScalarAsync();
            return Convert.ToDouble(value);
        });
        Assert.Equal(200.0, result);

        // Use DbTransact with rollback on exception
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _connection.Transact(async tx =>
            {
                var cmd = ((SqliteTransaction)tx).Connection!.CreateCommand();
                cmd.Transaction = (SqliteTransaction)tx;
                cmd.CommandText =
                    "INSERT INTO Orders (Id, CustomerId, Total) VALUES (@id, @cid, @total)";
                cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("@cid", "CUST-TX-FAIL");
                cmd.Parameters.AddWithValue("@total", 999.0);
                await cmd.ExecuteNonQueryAsync();
                throw new InvalidOperationException("Simulated failure");
            });
        });
        Assert.Equal("Simulated failure", exception.Message);

        // Verify rolled back
        var failCount = _connection.Scalar<long>(
            sql: "SELECT COUNT(*) FROM Orders WHERE CustomerId = 'CUST-TX-FAIL'"
        );
        Assert.Equal(0L, ((Result<long?, SqlError>.Ok<long?, SqlError>)failCount).Value);
    }

    [Fact]
    public void TransactionErrorHandling_InvalidOperationsInTransaction_ReturnsErrors()
    {
        using var tx = _connection.BeginTransaction();

        // Invalid SQL within transaction
        var badQuery = tx.Query<string>(sql: "SELECT FROM BAD SYNTAX", mapper: r => r.GetString(0));
        Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Error<
            IReadOnlyList<string>,
            SqlError
        >>(badQuery);
        var queryError = (
            (Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>)badQuery
        ).Value;
        Assert.NotEmpty(queryError.Message);

        // Invalid execute within transaction
        var badExec = tx.Execute(sql: "INSERT INTO NONEXISTENT VALUES ('x')");
        Assert.IsType<IntError>(badExec);

        // Invalid scalar within transaction
        var badScalar = tx.Scalar<long>(sql: "COMPLETELY INVALID SQL");
        Assert.IsType<Result<long?, SqlError>.Error<long?, SqlError>>(badScalar);

        // Null/empty SQL within transaction
        var emptyQuery = tx.Query<string>(sql: "", mapper: r => r.GetString(0));
        Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Error<
            IReadOnlyList<string>,
            SqlError
        >>(emptyQuery);

        var emptyExec = tx.Execute(sql: "  ");
        Assert.IsType<IntError>(emptyExec);

        var emptyScalar = tx.Scalar<long>(sql: "");
        Assert.IsType<Result<long?, SqlError>.Error<long?, SqlError>>(emptyScalar);
    }
}
