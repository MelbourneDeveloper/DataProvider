using System.Data;
using Generated;
using Microsoft.Data.Sqlite;
using Outcome;
using Selecta;

namespace Lql.TypeProvider.FSharp.Tests.Data;

/// <summary>
/// Seeds the test database with sample data using generated DataProvider extensions
/// </summary>
public static class TestDataSeeder
{
    /// <summary>
    /// Clears all test data from the database using generated delete extension
    /// </summary>
    /// <param name="transaction">The database transaction</param>
    /// <returns>Result indicating success or failure</returns>
    public static async Task<Result<int, SqlError>> ClearDataAsync(IDbTransaction transaction)
    {
        if (transaction.Connection is null)
            return new Result<int, SqlError>.Error<int, SqlError>(
                new SqlError("Transaction has no connection")
            );

        // Delete in order respecting foreign keys (Orders references Users)
        using (
            var cmd = new SqliteCommand(
                "DELETE FROM Orders",
                (SqliteConnection)transaction.Connection,
                (SqliteTransaction)transaction
            )
        )
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        using (
            var cmd = new SqliteCommand(
                "DELETE FROM Users",
                (SqliteConnection)transaction.Connection,
                (SqliteTransaction)transaction
            )
        )
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        using (
            var cmd = new SqliteCommand(
                "DELETE FROM Products",
                (SqliteConnection)transaction.Connection,
                (SqliteTransaction)transaction
            )
        )
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        using (
            var cmd = new SqliteCommand(
                "DELETE FROM Customer",
                (SqliteConnection)transaction.Connection,
                (SqliteTransaction)transaction
            )
        )
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        return new Result<int, SqlError>.Ok<int, SqlError>(0);
    }

    /// <summary>
    /// Seeds the database with test data using generated insert methods
    /// </summary>
    /// <param name="transaction">The database transaction</param>
    /// <returns>Result indicating success or failure</returns>
    public static async Task<Result<string, SqlError>> SeedDataAsync(IDbTransaction transaction)
    {
        // Clear existing data first
        var clearResult = await ClearDataAsync(transaction).ConfigureAwait(false);
        if (clearResult is Result<int, SqlError>.Error<int, SqlError> clearErr)
            return new Result<string, SqlError>.Error<string, SqlError>(clearErr.Value);

        // Insert Customers using generated extensions
        var c1 = await transaction
            .InsertCustomerAsync("c1", "Acme Corp", "acme@example.com", 10, "active")
            .ConfigureAwait(false);
        if (c1 is Result<int, SqlError>.Error<int, SqlError> c1Err)
            return new Result<string, SqlError>.Error<string, SqlError>(c1Err.Value);

        var c2 = await transaction
            .InsertCustomerAsync("c2", "Tech Corp", "tech@example.com", 5, "active")
            .ConfigureAwait(false);
        if (c2 is Result<int, SqlError>.Error<int, SqlError> c2Err)
            return new Result<string, SqlError>.Error<string, SqlError>(c2Err.Value);

        var c3 = await transaction
            .InsertCustomerAsync("c3", "New Corp", "new@example.com", 1, "pending")
            .ConfigureAwait(false);
        if (c3 is Result<int, SqlError>.Error<int, SqlError> c3Err)
            return new Result<string, SqlError>.Error<string, SqlError>(c3Err.Value);

        // Insert Users using generated extensions
        var u1 = await transaction
            .InsertUsersAsync(
                "u1",
                "Alice",
                "alice@example.com",
                30,
                "active",
                "admin",
                "2024-01-01"
            )
            .ConfigureAwait(false);
        if (u1 is Result<int, SqlError>.Error<int, SqlError> u1Err)
            return new Result<string, SqlError>.Error<string, SqlError>(u1Err.Value);

        var u2 = await transaction
            .InsertUsersAsync("u2", "Bob", "bob@example.com", 16, "active", "user", "2024-01-02")
            .ConfigureAwait(false);
        if (u2 is Result<int, SqlError>.Error<int, SqlError> u2Err)
            return new Result<string, SqlError>.Error<string, SqlError>(u2Err.Value);

        var u3 = await transaction
            .InsertUsersAsync(
                "u3",
                "Charlie",
                "charlie@example.com",
                25,
                "inactive",
                "user",
                "2024-01-03"
            )
            .ConfigureAwait(false);
        if (u3 is Result<int, SqlError>.Error<int, SqlError> u3Err)
            return new Result<string, SqlError>.Error<string, SqlError>(u3Err.Value);

        var u4 = await transaction
            .InsertUsersAsync(
                "u4",
                "Diana",
                "diana@example.com",
                15,
                "active",
                "admin",
                "2024-01-04"
            )
            .ConfigureAwait(false);
        if (u4 is Result<int, SqlError>.Error<int, SqlError> u4Err)
            return new Result<string, SqlError>.Error<string, SqlError>(u4Err.Value);

        // Insert Products using generated extensions
        var p1 = await transaction
            .InsertProductsAsync("p1", "Widget", 10.00, 100)
            .ConfigureAwait(false);
        if (p1 is Result<int, SqlError>.Error<int, SqlError> p1Err)
            return new Result<string, SqlError>.Error<string, SqlError>(p1Err.Value);

        var p2 = await transaction
            .InsertProductsAsync("p2", "Gadget", 25.50, 50)
            .ConfigureAwait(false);
        if (p2 is Result<int, SqlError>.Error<int, SqlError> p2Err)
            return new Result<string, SqlError>.Error<string, SqlError>(p2Err.Value);

        var p3 = await transaction
            .InsertProductsAsync("p3", "Gizmo", 5.00, 200)
            .ConfigureAwait(false);
        if (p3 is Result<int, SqlError>.Error<int, SqlError> p3Err)
            return new Result<string, SqlError>.Error<string, SqlError>(p3Err.Value);

        // Insert Orders using generated extensions (user 1 has 6 orders, user 2 has 1)
        var o1 = await transaction
            .InsertOrdersAsync("o1", "u1", "p1", 100.00, 90.00, 10.00, 0.00, "completed")
            .ConfigureAwait(false);
        if (o1 is Result<int, SqlError>.Error<int, SqlError> o1Err)
            return new Result<string, SqlError>.Error<string, SqlError>(o1Err.Value);

        var o2 = await transaction
            .InsertOrdersAsync("o2", "u1", "p2", 50.00, 45.00, 5.00, 0.00, "completed")
            .ConfigureAwait(false);
        if (o2 is Result<int, SqlError>.Error<int, SqlError> o2Err)
            return new Result<string, SqlError>.Error<string, SqlError>(o2Err.Value);

        var o3 = await transaction
            .InsertOrdersAsync("o3", "u1", "p1", 75.00, 68.00, 7.00, 0.00, "pending")
            .ConfigureAwait(false);
        if (o3 is Result<int, SqlError>.Error<int, SqlError> o3Err)
            return new Result<string, SqlError>.Error<string, SqlError>(o3Err.Value);

        var o4 = await transaction
            .InsertOrdersAsync("o4", "u1", "p3", 25.00, 22.50, 2.50, 0.00, "completed")
            .ConfigureAwait(false);
        if (o4 is Result<int, SqlError>.Error<int, SqlError> o4Err)
            return new Result<string, SqlError>.Error<string, SqlError>(o4Err.Value);

        var o5 = await transaction
            .InsertOrdersAsync("o5", "u1", "p2", 125.00, 112.50, 12.50, 0.00, "completed")
            .ConfigureAwait(false);
        if (o5 is Result<int, SqlError>.Error<int, SqlError> o5Err)
            return new Result<string, SqlError>.Error<string, SqlError>(o5Err.Value);

        var o6 = await transaction
            .InsertOrdersAsync("o6", "u1", "p1", 200.00, 180.00, 20.00, 0.00, "pending")
            .ConfigureAwait(false);
        if (o6 is Result<int, SqlError>.Error<int, SqlError> o6Err)
            return new Result<string, SqlError>.Error<string, SqlError>(o6Err.Value);

        var o7 = await transaction
            .InsertOrdersAsync("o7", "u2", "p3", 30.00, 27.00, 3.00, 0.00, "completed")
            .ConfigureAwait(false);
        if (o7 is Result<int, SqlError>.Error<int, SqlError> o7Err)
            return new Result<string, SqlError>.Error<string, SqlError>(o7Err.Value);

        return new Result<string, SqlError>.Ok<string, SqlError>("Test data seeded successfully");
    }
}
