using System.Collections.Immutable;
using System.Data;
using Generated;
using Microsoft.Data.Sqlite;
using Results;
using Selecta;

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities

namespace DataProvider.Example;

internal static class Program
{
    public static async Task Main(string[] _)
    {
        const string connectionString = "Data Source=invoices.db";

        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        // Try to force 32-bit integer mode
        using (var pragmaCmd = new SqliteCommand("PRAGMA legacy_alter_table = ON", connection))
        {
            await pragmaCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // Create all tables
        using (
            var command = new SqliteCommand(
                """
                CREATE TABLE IF NOT EXISTS Invoice (
                    Id INTEGER PRIMARY KEY,
                    InvoiceNumber TEXT NOT NULL,
                    InvoiceDate TEXT NOT NULL,
                    CustomerName TEXT NOT NULL,
                    CustomerEmail TEXT NULL,
                    TotalAmount REAL NOT NULL,
                    DiscountAmount REAL NULL,
                    Notes TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS InvoiceLine (
                    Id INTEGER PRIMARY KEY,
                    InvoiceId SMALLINT NOT NULL,
                    Description TEXT NOT NULL,
                    Quantity REAL NOT NULL,
                    UnitPrice REAL NOT NULL,
                    Amount REAL NOT NULL,
                    DiscountPercentage REAL NULL,
                    Notes TEXT NULL,
                    FOREIGN KEY (InvoiceId) REFERENCES Invoice (Id)
                );

                CREATE TABLE IF NOT EXISTS Customer (
                    Id INTEGER PRIMARY KEY,
                    CustomerName TEXT NOT NULL,
                    Email TEXT NULL,
                    Phone TEXT NULL,
                    CreatedDate TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Address (
                    Id INTEGER PRIMARY KEY,
                    CustomerId SMALLINT NOT NULL,
                    Street TEXT NOT NULL,
                    City TEXT NOT NULL,
                    State TEXT NOT NULL,
                    ZipCode TEXT NOT NULL,
                    Country TEXT NOT NULL,
                    FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
                );

                CREATE TABLE IF NOT EXISTS Orders (
                    Id INTEGER PRIMARY KEY,
                    OrderNumber TEXT NOT NULL,
                    OrderDate TEXT NOT NULL,
                    CustomerId SMALLINT NOT NULL,
                    TotalAmount REAL NOT NULL,
                    Status TEXT NOT NULL,
                    FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
                );

                CREATE TABLE IF NOT EXISTS OrderItem (
                    Id INTEGER PRIMARY KEY,
                    OrderId SMALLINT NOT NULL,
                    ProductName TEXT NOT NULL,
                    Quantity REAL NOT NULL,
                    Price REAL NOT NULL,
                    Subtotal REAL NOT NULL,
                    FOREIGN KEY (OrderId) REFERENCES Orders (Id)
                );
                """,
                connection
            )
        )
        {
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // Clear existing data
        using (
            var command = new SqliteCommand(
                "DELETE FROM OrderItem; DELETE FROM Orders; DELETE FROM Address; DELETE FROM Customer; DELETE FROM InvoiceLine; DELETE FROM Invoice;",
                connection
            )
        )
        {
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // Insert sample data using generated insert methods wrapped in Transact()
        var insertResult = await connection
            .Transact<Result<string, SqlError>>(
                async (transaction) =>
                {
                    // Insert Customers
                    var customer1Result = await connection
                        .InsertCustomerAsync(
                            "Acme Corp",
                            "contact@acme.com",
                            "555-0100",
                            "2024-01-01"
                        )
                        .ConfigureAwait(false);
                    if (customer1Result is not Result<long, SqlError>.Success customer1Success)
                        return new Result<string, SqlError>.Failure(
                            (customer1Result as Result<long, SqlError>.Failure)!.ErrorValue
                        );

                    var customer2Result = await connection
                        .InsertCustomerAsync(
                            "Tech Solutions",
                            "info@techsolutions.com",
                            "555-0200",
                            "2024-01-02"
                        )
                        .ConfigureAwait(false);
                    if (customer2Result is not Result<long, SqlError>.Success customer2Success)
                        return new Result<string, SqlError>.Failure(
                            (customer2Result as Result<long, SqlError>.Failure)!.ErrorValue
                        );

                    // Insert Invoice
                    var invoiceResult = await connection
                        .InsertInvoiceAsync(
                            "INV-001",
                            "2024-01-15",
                            "Acme Corp",
                            "billing@acme.com",
                            1250.00,
                            50.00,
                            "Sample invoice"
                        )
                        .ConfigureAwait(false);
                    if (invoiceResult is not Result<long, SqlError>.Success invoiceSuccess)
                        return new Result<string, SqlError>.Failure(
                            (invoiceResult as Result<long, SqlError>.Failure)!.ErrorValue
                        );

                    // Insert InvoiceLines
                    var invoiceLine1Result = await connection
                        .InsertInvoiceLineAsync(
                            invoiceSuccess.Value,
                            "Software License",
                            1.0,
                            1000.00,
                            1000.00,
                            null,
                            null
                        )
                        .ConfigureAwait(false);
                    if (invoiceLine1Result is not Result<long, SqlError>.Success)
                        return new Result<string, SqlError>.Failure(
                            (invoiceLine1Result as Result<long, SqlError>.Failure)!.ErrorValue
                        );

                    var invoiceLine2Result = await connection
                        .InsertInvoiceLineAsync(
                            invoiceSuccess.Value,
                            "Support Package",
                            1.0,
                            250.00,
                            250.00,
                            null,
                            "First year"
                        )
                        .ConfigureAwait(false);
                    if (invoiceLine2Result is not Result<long, SqlError>.Success)
                        return new Result<string, SqlError>.Failure(
                            (invoiceLine2Result as Result<long, SqlError>.Failure)!.ErrorValue
                        );

                    // Insert Addresses
                    var address1Result = await connection
                        .InsertAddressAsync(
                            customer1Success.Value,
                            "123 Business Ave",
                            "New York",
                            "NY",
                            "10001",
                            "USA"
                        )
                        .ConfigureAwait(false);
                    if (address1Result is not Result<long, SqlError>.Success)
                        return new Result<string, SqlError>.Failure(
                            (address1Result as Result<long, SqlError>.Failure)!.ErrorValue
                        );

                    var address2Result = await connection
                        .InsertAddressAsync(
                            customer1Success.Value,
                            "456 Main St",
                            "Albany",
                            "NY",
                            "12201",
                            "USA"
                        )
                        .ConfigureAwait(false);
                    if (address2Result is not Result<long, SqlError>.Success)
                        return new Result<string, SqlError>.Failure(
                            (address2Result as Result<long, SqlError>.Failure)!.ErrorValue
                        );

                    var address3Result = await connection
                        .InsertAddressAsync(
                            customer2Success.Value,
                            "789 Tech Blvd",
                            "San Francisco",
                            "CA",
                            "94105",
                            "USA"
                        )
                        .ConfigureAwait(false);
                    if (address3Result is not Result<long, SqlError>.Success)
                        return new Result<string, SqlError>.Failure(
                            (address3Result as Result<long, SqlError>.Failure)!.ErrorValue
                        );

                    // Insert Orders
                    var order1Result = await connection
                        .InsertOrdersAsync(
                            "ORD-001",
                            "2024-01-10",
                            customer1Success.Value,
                            500.00,
                            "Completed"
                        )
                        .ConfigureAwait(false);
                    if (order1Result is not Result<long, SqlError>.Success order1Success)
                        return new Result<string, SqlError>.Failure(
                            (order1Result as Result<long, SqlError>.Failure)!.ErrorValue
                        );

                    var order2Result = await connection
                        .InsertOrdersAsync(
                            "ORD-002",
                            "2024-01-11",
                            customer2Success.Value,
                            750.00,
                            "Processing"
                        )
                        .ConfigureAwait(false);
                    if (order2Result is not Result<long, SqlError>.Success order2Success)
                        return new Result<string, SqlError>.Failure(
                            (order2Result as Result<long, SqlError>.Failure)!.ErrorValue
                        );

                    // Insert OrderItems
                    var orderItem1Result = await connection
                        .InsertOrderItemAsync(order1Success.Value, "Widget A", 2.0, 100.00, 200.00)
                        .ConfigureAwait(false);
                    if (orderItem1Result is not Result<long, SqlError>.Success)
                        return new Result<string, SqlError>.Failure(
                            (orderItem1Result as Result<long, SqlError>.Failure)!.ErrorValue
                        );

                    var orderItem2Result = await connection
                        .InsertOrderItemAsync(order1Success.Value, "Widget B", 3.0, 100.00, 300.00)
                        .ConfigureAwait(false);
                    if (orderItem2Result is not Result<long, SqlError>.Success)
                        return new Result<string, SqlError>.Failure(
                            (orderItem2Result as Result<long, SqlError>.Failure)!.ErrorValue
                        );

                    var orderItem3Result = await connection
                        .InsertOrderItemAsync(
                            order2Success.Value,
                            "Service Package",
                            1.0,
                            750.00,
                            750.00
                        )
                        .ConfigureAwait(false);
                    if (orderItem3Result is not Result<long, SqlError>.Success)
                        return new Result<string, SqlError>.Failure(
                            (orderItem3Result as Result<long, SqlError>.Failure)!.ErrorValue
                        );

                    return new Result<string, SqlError>.Success(
                        "Data inserted successfully using generated methods"
                    );
                }
            )
            .ConfigureAwait(false);

        Console.WriteLine("✅ Sample data processed within transaction");

        // Test all queries to verify source generator works with different table names
        Console.WriteLine("=== Testing GetInvoicesAsync ===");
        var invoiceResult = await connection
            .GetInvoicesAsync("Acme Corp", "2024-01-01", "2024-12-31")
            .ConfigureAwait(false);
        if (invoiceResult is Result<ImmutableList<Invoice>, SqlError>.Success invOk)
        {
            var preview = string.Join(
                " | ",
                invOk
                    .Value.Take(3)
                    .Select(i =>
                        $"{i.InvoiceNumber} on {i.InvoiceDate} → {i.CustomerName} ({i.TotalAmount:C})"
                    )
            );
            Console.WriteLine($"Invoices fetched: {invOk.Value.Count}. Preview: {preview}");
        }
        else if (invoiceResult is Result<ImmutableList<Invoice>, SqlError>.Failure invErr)
        {
            Console.WriteLine($"Error querying invoices: {invErr.ErrorValue.Message}");
        }

        Console.WriteLine("\n=== Testing GetCustomersLqlAsync ===");
        var customerResult = await connection.GetCustomersLqlAsync(null).ConfigureAwait(false);
        if (customerResult is Result<ImmutableList<Customer>, SqlError>.Success custOk)
        {
            var preview = string.Join(
                " | ",
                custOk
                    .Value.Take(3)
                    .Select(c => $"{c.CustomerName} <{c.Email}> since {c.CreatedDate}")
            );
            Console.WriteLine($"Customers fetched: {custOk.Value.Count}. Preview: {preview}");
        }
        else if (customerResult is Result<ImmutableList<Customer>, SqlError>.Failure custErr)
        {
            Console.WriteLine($"Error querying customers: {custErr.ErrorValue.Message}");
        }

        Console.WriteLine("\n=== Testing GetOrdersAsync ===");
        var orderResult = await connection
            .GetOrdersAsync(1, "Completed", "2024-01-01", "2024-12-31")
            .ConfigureAwait(false);
        if (orderResult is Result<ImmutableList<Order>, SqlError>.Success ordOk)
        {
            var preview = string.Join(
                " | ",
                ordOk
                    .Value.Take(3)
                    .Select(o => $"{o.OrderNumber} {o.Status} ({o.TotalAmount:C}) on {o.OrderDate}")
            );
            Console.WriteLine($"Orders fetched: {ordOk.Value.Count}. Preview: {preview}");
        }
        else if (orderResult is Result<ImmutableList<Order>, SqlError>.Failure ordErr)
        {
            Console.WriteLine($"Error querying orders: {ordErr.ErrorValue.Message}");
        }

        // ---------------------------------------------------------------------
        // Additional examples: Using SqlStatementBuilder + GetRecords<T>
        // ---------------------------------------------------------------------
        Console.WriteLine("\n=== Testing GetRecords<T> with SqlStatement (SQLite) ===");

        // Build a simple SELECT using the Selecta.SqlStatementBuilder (no reflection).
        // Example: SELECT Id, CustomerName, Email, CreatedDate FROM Customer WHERE CustomerName LIKE '%Acme%'
        var stmt = new SqlStatementBuilder()
            .AddTable("Customer")
            .AddSelectColumn(ColumnInfo.Named("Id"))
            .AddSelectColumn(ColumnInfo.Named("CustomerName"))
            .AddSelectColumn(ColumnInfo.Named("Email"))
            .AddSelectColumn(ColumnInfo.Named("CreatedDate"))
            .AddWhereCondition(
                WhereCondition.Comparison(
                    ColumnInfo.Named("CustomerName"),
                    ComparisonOperator.Like,
                    "'%Acme%'"
                )
            )
            .Build();

        // Note: manual GetRecords mapping demo removed to rely on generated methods above.
    }
}
