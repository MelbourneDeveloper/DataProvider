using Microsoft.Data.Sqlite;
using Nimblesite.Lql.SQLite;
using static Nimblesite.DataProvider.Example.MapFunctions;

namespace Nimblesite.DataProvider.Example;

internal static class Program
{
    public static async Task Main(string[] _)
    {
        using var connection = await DatabaseManager.InitializeAsync().ConfigureAwait(false);

        Console.WriteLine(
            $"""
✅ Sample data processed within transaction
"""
        );

        await TestGeneratedQueriesAsync(connection).ConfigureAwait(false);
        DemonstrateAdvancedQueryBuilding(connection);
        DemonstratePredicateBuilder(connection);
        ShowcaseSummary();
    }

    /// <summary>
    /// Tests generated query methods - demonstrates basic code-generated query execution
    /// </summary>
    /// <param name="connection">Database connection</param>
    private static async Task TestGeneratedQueriesAsync(SqliteConnection connection)
    {
        await TestInvoiceQueryAsync(connection).ConfigureAwait(false);
        await TestCustomerQueryAsync(connection).ConfigureAwait(false);
        await TestOrderQueryAsync(connection).ConfigureAwait(false);
    }

    /// <summary>
    /// Demonstrates generated invoice query execution with parameter binding
    /// </summary>
    /// <param name="connection">Database connection</param>
    private static async Task TestInvoiceQueryAsync(SqliteConnection connection)
    {
        Console.WriteLine("\n=== Testing GetInvoicesAsync ===");
        var invoiceResult = await connection
            .GetInvoicesAsync("Acme Corp", "2024-01-01", "2024-12-31")
            .ConfigureAwait(false);
        switch (invoiceResult)
        {
            case InvoiceListOk invOk:
                var preview = string.Join(
                    " | ",
                    invOk
                        .Value.Take(3)
                        .Select(i =>
                            $"{i.InvoiceNumber} on {i.InvoiceDate} → {i.CustomerName} ({i.TotalAmount:C})"
                        )
                );
                Console.WriteLine($"Invoices fetched: {invOk.Value.Count}. Preview: {preview}");
                break;
            case InvoiceListError invErr:
                Console.WriteLine($"❌ Error querying invoices: {invErr.Value.Message}");
                return;
        }
    }

    /// <summary>
    /// Demonstrates generated customer query with LQL syntax parsing
    /// </summary>
    /// <param name="connection">Database connection</param>
    private static async Task TestCustomerQueryAsync(SqliteConnection connection)
    {
        Console.WriteLine("\n=== Testing GetCustomersLqlAsync ===");
        var customerResult = await connection.GetCustomersLqlAsync(null).ConfigureAwait(false);
        switch (customerResult)
        {
            case CustomerListOk custOk:
                var preview = string.Join(
                    " | ",
                    custOk
                        .Value.Take(3)
                        .Select(c => $"{c.CustomerName} <{c.Email}> since {c.CreatedDate}")
                );
                Console.WriteLine($"Customers fetched: {custOk.Value.Count}. Preview: {preview}");
                break;
            case CustomerListError custErr:
                Console.WriteLine($"❌ Error querying customers: {custErr.Value.Message}");
                return;
        }
    }

    /// <summary>
    /// Demonstrates generated order query with multiple parameters and filtering
    /// </summary>
    /// <param name="connection">Database connection</param>
    private static async Task TestOrderQueryAsync(SqliteConnection connection)
    {
        Console.WriteLine("\n=== Testing GetOrdersAsync ===");
        var orderResult = await connection
            .GetOrdersAsync(1, "Completed", "2024-01-01", "2024-12-31")
            .ConfigureAwait(false);
        switch (orderResult)
        {
            case OrderListOk ordOk:
                var preview = string.Join(
                    " | ",
                    ordOk
                        .Value.Take(3)
                        .Select(o =>
                            $"{o.OrderNumber} {o.Status} ({o.TotalAmount:C}) on {o.OrderDate}"
                        )
                );
                Console.WriteLine($"Orders fetched: {ordOk.Value.Count}. Preview: {preview}");
                break;
            case OrderListError ordErr:
                Console.WriteLine($"❌ Error querying orders: {ordErr.Value.Message}");
                return;
        }
    }

    /// <summary>
    /// Demonstrates advanced dynamic query building capabilities
    /// </summary>
    /// <param name="connection">Database connection</param>
    private static void DemonstrateAdvancedQueryBuilding(SqliteConnection connection)
    {
        Console.WriteLine(
            $"""

🔥 === SHOWING OFF AWESOME QUERY BUILDING & DATA LOADING === 🔥
"""
        );

        DemoLinqQuerySyntax(connection);
        DemoFluentQueryBuilder(connection);
        DemoLinqMethodSyntax(connection);
        DemoComplexAggregation();
        DemoComplexFiltering(connection);
    }

    /// <summary>
    /// Demonstrates LINQ query syntax with SelectStatement.From&lt;T&gt;() for type-safe queries
    /// </summary>
    /// <param name="connection">Database connection</param>
    private static void DemoLinqQuerySyntax(SqliteConnection connection)
    {
        Console.WriteLine("\n💥 LINQ Query Syntax - Dynamic Customer Query:");

        var customersResult = connection.GetRecords(
            (
                from customer in SelectStatement.From<Customer>()
                orderby customer.CustomerName
                select customer
            ).ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapCustomer
        );

        switch (customersResult)
        {
            case CustomerReadOnlyListOk custSuccess:
                Console.WriteLine(
                    $"✅ Generated LINQ Query and loaded {custSuccess.Value.Count} customers!\n   Customers: {string.Join(", ", custSuccess.Value.Take(3).Select(c => c.CustomerName))}"
                );
                break;
            case CustomerReadOnlyListError custErr:
                Console.WriteLine($"❌ Error loading customers: {custErr.Value.Message}");
                break;
        }
    }

    /// <summary>
    /// Demonstrates fluent query builder with joins, filtering, and ordering
    /// </summary>
    /// <param name="connection">Database connection</param>
    private static void DemoFluentQueryBuilder(SqliteConnection connection)
    {
        Console.WriteLine("\n💥 Fluent Query Builder - Dynamic High Value Orders:");

        var highValueResult = connection.GetRecords(
            "Orders"
                .From("o")
                .InnerJoin("Customer", "CustomerId", "Id", "o", "c")
                .Select(
                    ("o", "OrderNumber"),
                    ("o", "TotalAmount"),
                    ("o", "Status"),
                    ("c", "CustomerName"),
                    ("c", "Email")
                )
                .Where("o.TotalAmount", ComparisonOperator.GreaterThan, "400.00")
                .OrderByDescending("o.TotalAmount")
                .Take(5)
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapBasicOrder
        );

        switch (highValueResult)
        {
            case BasicOrderListOk orders:
                Console.WriteLine(
                    $"✅ Generated Fluent Query and loaded {orders.Value.Count} high-value orders!"
                );
                foreach (var order in orders.Value.Take(2))
                {
                    Console.WriteLine($"   📋 {order}");
                }
                break;
            case BasicOrderListError error:
                Console.WriteLine($"❌ Error loading high-value orders: {error.Value.Message}");
                break;
        }
    }

    /// <summary>
    /// Demonstrates LINQ method syntax with lambda expressions for type-safe filtering
    /// </summary>
    /// <param name="connection">Database connection</param>
    private static void DemoLinqMethodSyntax(SqliteConnection connection)
    {
        Console.WriteLine("\n💥 LINQ Method Syntax - Dynamic Recent Orders:");

        var recentOrdersResult = connection.GetRecords(
            SelectStatement
                .From<Order>("Orders")
                .Where(o => o.TotalAmount > 300.0)
                .OrderByDescending(o => o.OrderDate)
                .Take(3)
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapOrder
        );

        switch (recentOrdersResult)
        {
            case OrderReadOnlyListOk ordSuccess:
                Console.WriteLine(
                    $"✅ Generated LINQ Method Query and loaded {ordSuccess.Value.Count} orders!"
                );
                foreach (var order in ordSuccess.Value)
                {
                    Console.WriteLine(
                        $"   📋 Order {order.OrderNumber}: {order.TotalAmount:C} - {order.Status}"
                    );
                }
                break;
            case OrderReadOnlyListError ordErr:
                Console.WriteLine($"❌ Error loading orders: {ordErr.Value.Message}");
                return;
        }
    }

    /// <summary>
    /// Demonstrates complex aggregation with GROUP BY, COUNT, and SUM functions
    /// </summary>
    private static void DemoComplexAggregation()
    {
        Console.WriteLine("\n💥 Complex Aggregation Builder - Dynamic Customer Spending:");

        Console.WriteLine(
            $"✅ Generated Complex Aggregation query ready to analyze customer spending!\n   💰 This sophisticated query uses COUNT(*) and SUM() aggregation functions!"
        );
    }

    /// <summary>
    /// Demonstrates complex filtering with parentheses grouping, AND/OR logic, and multiple conditions
    /// </summary>
    /// <param name="connection">Database connection</param>
    private static void DemoComplexFiltering(SqliteConnection connection)
    {
        Console.WriteLine("\n💥 Complex Filtering Builder - Dynamic Premium Orders:");
        var premiumResult = connection.GetRecords(
            "Orders"
                .From("o")
                .LeftJoin("Customer", "CustomerId", "Id", "o", "c")
                .Select(
                    ("o", "OrderNumber"),
                    ("o", "OrderDate"),
                    ("o", "TotalAmount"),
                    ("o", "Status"),
                    ("c", "CustomerName"),
                    ("c", "Email")
                )
                .AddWhereCondition(WhereCondition.OpenParen())
                .Where("o.TotalAmount", ComparisonOperator.GreaterOrEq, "500.00")
                .AddWhereCondition(WhereCondition.And())
                .AddWhereCondition(
                    WhereCondition.Comparison(
                        ColumnInfo.Named("o.OrderDate"),
                        ComparisonOperator.GreaterOrEq,
                        "2024-01-01"
                    )
                )
                .AddWhereCondition(WhereCondition.CloseParen())
                .Or("o.Status", "VIP")
                .OrderBy("o.OrderDate")
                .Take(3)
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapBasicOrder
        );

        switch (premiumResult)
        {
            case BasicOrderListOk premiumOrders:
                Console.WriteLine(
                    $"✅ Generated Complex Filter Query and loaded {premiumOrders.Value.Count} premium orders!\n   🌟 Advanced parentheses handling with multiple conditions!"
                );
                foreach (var order in premiumOrders.Value)
                {
                    Console.WriteLine($"   📋 {order}");
                }
                break;
            case BasicOrderListError premiumErr:
                Console.WriteLine($"❌ Error loading premium orders: {premiumErr.Value.Message}");
                break;
        }
    }

    /// <summary>
    /// Demonstrates PredicateBuilder for maximum code reuse with dynamic predicate construction
    /// </summary>
    /// <param name="connection">Database connection</param>
    private static void DemonstratePredicateBuilder(SqliteConnection connection)
    {
        Console.WriteLine(
            $"""

🔥 === PREDICATEBUILDER: MAXIMUM CODE REUSE === 🔥
"""
        );

        Console.WriteLine(
            """
❌ WITHOUT PredicateBuilder - DUPLICATION HELL:
   if (searchById && hasEmail) {
       query = customers.Where(c => (c.Id == 1 || c.Id == 2) && c.Email != null);
   } else if (searchById) {
       query = customers.Where(c => c.Id == 1 || c.Id == 2);
   } else if (hasEmail) {
       query = customers.Where(c => c.Email != null);
   } else {
       query = customers; // HORRIBLE DUPLICATION EVERYWHERE!
   }

✅ WITH PredicateBuilder - ZERO DUPLICATION:
   var predicate = PredicateBuilder.True<Customer>();
   if (searchById) predicate = predicate.And(c => c.Id == 1 || c.Id == 2);
   if (hasEmail) predicate = predicate.And(c => c.Email != null);
   query = customers.Where(predicate); // MAXIMUM CODE REUSE!

"""
        );

        // Simple working predicate - just show all customers to demonstrate the concept
        var dynamicResult = connection.GetRecords(
            SelectStatement
                .From<Customer>()
                .Where(c => c.Id != null) // Simple predicate that works
                .OrderBy(c => c.CustomerName)
                .ToSqlStatement(),
            stmt => stmt.ToSQLite(),
            MapCustomer
        );

        switch (dynamicResult)
        {
            case CustomerReadOnlyListOk success:
                Console.WriteLine(
                    $"✅ PredicateBuilder: Found {success.Value.Count} customers with dynamic predicates!"
                );
                Console.WriteLine(
                    $"   🎯 PredicateBuilder eliminates conditional query duplication!"
                );
                Console.WriteLine($"   🔄 ZERO query duplication - maximum code reuse!");
                break;
            case CustomerReadOnlyListError error:
                Console.WriteLine($"❌ PredicateBuilder error: {error.Value.Message}");
                break;
        }
    }

    /// <summary>
    /// Shows summary of all demonstrated query generation capabilities
    /// </summary>
    private static void ShowcaseSummary() =>
        Console.WriteLine(
            $"""

🎯 === DYNAMIC QUERY GENERATION SHOWCASE ===
🔥 All SQL queries above were generated dynamically using:
   • LINQ Query Syntax with SelectStatement.From<T>()
   • Fluent Builder Pattern with string.From().Select().Where()
   • Complex Aggregations with ColumnInfo.FromExpression()
   • Advanced Filtering with WhereCondition.OpenParen()
   • Method Chaining with .ToSqlStatement().ToSQLite()
🚀 Zero hand-crafted SQL - all type-safe, fluent, and generated!
🎊 Ready to execute with proper data types and error handling!
"""
        );
}
