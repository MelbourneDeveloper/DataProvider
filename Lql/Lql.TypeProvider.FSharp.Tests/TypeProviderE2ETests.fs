module Lql.TypeProvider.Tests

open System
open Microsoft.Data.Sqlite
open Xunit
open Lql
open Lql.TypeProvider

// =============================================================================
// COMPILE-TIME VALIDATED LQL QUERIES
// These are validated at COMPILE TIME by the F# type provider
// Invalid LQL will cause a COMPILATION ERROR, not a runtime error
// =============================================================================

// Basic select queries
type SelectAll = LqlCommand<"Customer |> select(*)">
type SelectColumns = LqlCommand<"users |> select(users.id, users.name, users.email)">
type SelectWithAlias = LqlCommand<"users |> select(users.id, users.name as username)">

// Filter queries
type FilterSimple = LqlCommand<"users |> filter(fn(row) => row.users.age > 18) |> select(users.name)">
type FilterComplex = LqlCommand<"users |> filter(fn(row) => row.users.age > 18 and row.users.status = 'active') |> select(*)">
type FilterOr = LqlCommand<"users |> filter(fn(row) => row.users.age < 18 or row.users.role = 'admin') |> select(*)">

// Join queries
type JoinSimple = LqlCommand<"users |> join(orders, on = users.id = orders.user_id) |> select(users.name, orders.total)">
type JoinLeft = LqlCommand<"users |> left_join(orders, on = users.id = orders.user_id) |> select(users.name, orders.total)">
type JoinMultiple = LqlCommand<"users |> join(orders, on = users.id = orders.user_id) |> join(products, on = orders.product_id = products.id) |> select(users.name, products.name)">

// Aggregation queries
type GroupBy = LqlCommand<"orders |> group_by(orders.user_id) |> select(orders.user_id, count(*) as order_count)">
type Aggregates = LqlCommand<"orders |> group_by(orders.status) |> select(orders.status, sum(orders.total) as total_sum, avg(orders.total) as avg_total)">
type Having = LqlCommand<"orders |> group_by(orders.user_id) |> having(fn(g) => count(*) > 5) |> select(orders.user_id, count(*) as cnt)">

// Order and limit
type OrderBy = LqlCommand<"users |> order_by(users.name asc) |> select(*)">
type OrderByDesc = LqlCommand<"users |> order_by(users.created_at desc) |> select(*)">
type Limit = LqlCommand<"users |> order_by(users.id) |> limit(10) |> select(*)">
type Offset = LqlCommand<"users |> order_by(users.id) |> limit(10) |> offset(20) |> select(*)">

// Arithmetic expressions
type ArithmeticBasic = LqlCommand<"products |> select(products.price * products.quantity as total)">
type ArithmeticComplex = LqlCommand<"orders |> select(orders.subtotal + orders.tax - orders.discount as final_total)">

// =============================================================================
// E2E TEST FIXTURES - Test the type provider with REAL SQLite databases
// =============================================================================

module TestFixtures =
    let createTestDatabase() =
        let conn = new SqliteConnection("Data Source=:memory:")
        conn.Open()

        // Create test tables
        use cmd = new SqliteCommand("""
            CREATE TABLE Customer (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT,
                age INTEGER,
                status TEXT DEFAULT 'active'
            );

            CREATE TABLE users (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT,
                age INTEGER,
                status TEXT DEFAULT 'active',
                role TEXT DEFAULT 'user',
                created_at TEXT
            );

            CREATE TABLE orders (
                id INTEGER PRIMARY KEY,
                user_id INTEGER,
                product_id INTEGER,
                total REAL,
                subtotal REAL,
                tax REAL,
                discount REAL,
                status TEXT,
                FOREIGN KEY (user_id) REFERENCES users(id)
            );

            CREATE TABLE products (
                id INTEGER PRIMARY KEY,
                name TEXT,
                price REAL,
                quantity INTEGER
            );

            -- Insert test data
            INSERT INTO Customer (id, name, email, age, status) VALUES
                (1, 'Acme Corp', 'acme@example.com', 10, 'active'),
                (2, 'Tech Corp', 'tech@example.com', 5, 'active'),
                (3, 'Old Corp', 'old@example.com', 50, 'inactive');

            INSERT INTO users (id, name, email, age, status, role, created_at) VALUES
                (1, 'Alice', 'alice@example.com', 30, 'active', 'admin', '2024-01-01'),
                (2, 'Bob', 'bob@example.com', 17, 'active', 'user', '2024-01-02'),
                (3, 'Charlie', 'charlie@example.com', 25, 'inactive', 'user', '2024-01-03'),
                (4, 'Diana', 'diana@example.com', 16, 'active', 'admin', '2024-01-04');

            INSERT INTO orders (id, user_id, product_id, total, subtotal, tax, discount, status) VALUES
                (1, 1, 1, 100.0, 90.0, 15.0, 5.0, 'completed'),
                (2, 1, 2, 200.0, 180.0, 30.0, 10.0, 'completed'),
                (3, 2, 1, 50.0, 45.0, 7.5, 2.5, 'pending'),
                (4, 1, 3, 150.0, 135.0, 22.5, 7.5, 'completed'),
                (5, 1, 1, 75.0, 67.5, 11.25, 3.75, 'completed'),
                (6, 1, 2, 300.0, 270.0, 45.0, 15.0, 'completed'),
                (7, 1, 3, 125.0, 112.5, 18.75, 6.25, 'completed');

            INSERT INTO products (id, name, price, quantity) VALUES
                (1, 'Widget', 10.0, 100),
                (2, 'Gadget', 25.0, 50),
                (3, 'Gizmo', 15.0, 75);
        """, conn)
        cmd.ExecuteNonQuery() |> ignore
        conn

    let executeQuery (conn: SqliteConnection) (sql: string) =
        use cmd = new SqliteCommand(sql, conn)
        use reader = cmd.ExecuteReader()
        let results = ResizeArray<Map<string, obj>>()
        while reader.Read() do
            let row =
                [| for i in 0 .. reader.FieldCount - 1 ->
                    let name = reader.GetName(i)
                    let value = if reader.IsDBNull(i) then box DBNull.Value else reader.GetValue(i)
                    (name, value) |]
                |> Map.ofArray
            results.Add(row)
        results |> List.ofSeq

// =============================================================================
// E2E TESTS - Comprehensive tests for the F# Type Provider
// =============================================================================

[<Collection("TypeProvider")>]
type TypeProviderCompileTimeValidationTests() =

    [<Fact>]
    member _.``Type provider generates Query property for simple select``() =
        Assert.Equal("Customer |> select(*)", SelectAll.Query)

    [<Fact>]
    member _.``Type provider generates Sql property for simple select``() =
        Assert.NotNull(SelectAll.Sql)
        Assert.Contains("SELECT", SelectAll.Sql.ToUpperInvariant())

    [<Fact>]
    member _.``Type provider generates correct SQL for column selection``() =
        let sql = SelectColumns.Sql.ToUpperInvariant()
        Assert.Contains("SELECT", sql)
        Assert.Contains("USERS", sql)

    [<Fact>]
    member _.``Type provider generates SQL with alias``() =
        let sql = SelectWithAlias.Sql
        Assert.Contains("AS", sql.ToUpperInvariant())

[<Collection("TypeProvider")>]
type TypeProviderFilterTests() =

    [<Fact>]
    member _.``Filter query generates WHERE clause``() =
        let sql = FilterSimple.Sql.ToUpperInvariant()
        Assert.Contains("WHERE", sql)

    [<Fact>]
    member _.``Complex filter with AND generates correct SQL``() =
        let sql = FilterComplex.Sql.ToUpperInvariant()
        Assert.Contains("WHERE", sql)
        Assert.Contains("AND", sql)

    [<Fact>]
    member _.``Filter with OR generates correct SQL``() =
        let sql = FilterOr.Sql.ToUpperInvariant()
        Assert.Contains("WHERE", sql)
        Assert.Contains("OR", sql)

[<Collection("TypeProvider")>]
type TypeProviderJoinTests() =

    [<Fact>]
    member _.``Simple join generates JOIN clause``() =
        let sql = JoinSimple.Sql.ToUpperInvariant()
        Assert.Contains("JOIN", sql)
        Assert.Contains("ON", sql)

    [<Fact>]
    member _.``Left join generates LEFT JOIN clause``() =
        let sql = JoinLeft.Sql.ToUpperInvariant()
        Assert.Contains("LEFT", sql)
        Assert.Contains("JOIN", sql)

    [<Fact>]
    member _.``Multiple joins are chained correctly``() =
        let sql = JoinMultiple.Sql.ToUpperInvariant()
        // Should have at least 2 JOINs
        let joinCount = sql.Split([|"JOIN"|], StringSplitOptions.None).Length - 1
        Assert.True(joinCount >= 2, sprintf "Expected at least 2 JOINs but got %d" joinCount)

[<Collection("TypeProvider")>]
type TypeProviderAggregationTests() =

    [<Fact>]
    member _.``Group by generates GROUP BY clause``() =
        let sql = GroupBy.Sql.ToUpperInvariant()
        Assert.Contains("GROUP BY", sql)
        Assert.Contains("COUNT", sql)

    [<Fact>]
    member _.``Multiple aggregates work correctly``() =
        let sql = Aggregates.Sql.ToUpperInvariant()
        Assert.Contains("SUM", sql)
        Assert.Contains("AVG", sql)

    [<Fact>]
    member _.``Having clause generates HAVING``() =
        let sql = Having.Sql.ToUpperInvariant()
        Assert.Contains("HAVING", sql)

[<Collection("TypeProvider")>]
type TypeProviderOrderingTests() =

    [<Fact>]
    member _.``Order by generates ORDER BY clause``() =
        let sql = OrderBy.Sql.ToUpperInvariant()
        Assert.Contains("ORDER BY", sql)

    [<Fact>]
    member _.``Order by desc includes DESC``() =
        let sql = OrderByDesc.Sql.ToUpperInvariant()
        Assert.Contains("DESC", sql)

    [<Fact>]
    member _.``Limit generates LIMIT clause``() =
        let sql = Limit.Sql.ToUpperInvariant()
        Assert.Contains("LIMIT", sql)

    [<Fact>]
    member _.``Offset generates OFFSET clause``() =
        let sql = Offset.Sql.ToUpperInvariant()
        Assert.Contains("OFFSET", sql)

[<Collection("TypeProvider")>]
type TypeProviderArithmeticTests() =

    [<Fact>]
    member _.``Basic arithmetic in select``() =
        let sql = ArithmeticBasic.Sql
        Assert.Contains("*", sql) // multiplication

    [<Fact>]
    member _.``Complex arithmetic with multiple operators``() =
        let sql = ArithmeticComplex.Sql
        Assert.Contains("+", sql)
        Assert.Contains("-", sql)

[<Collection("TypeProvider")>]
type TypeProviderE2EExecutionTests() =

    [<Fact>]
    member _.``Execute simple select against real SQLite database``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn SelectAll.Sql
        Assert.Equal(3, results.Length)

    [<Fact>]
    member _.``Execute filter query and verify results``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn FilterSimple.Sql
        // Should return users with age > 18 (Alice=30, Charlie=25)
        Assert.Equal(2, results.Length)

    [<Fact>]
    member _.``Execute join query and verify results``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn JoinSimple.Sql
        // Should return joined user-order records
        Assert.True(results.Length > 0)

    [<Fact>]
    member _.``Execute group by query and verify aggregation``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn GroupBy.Sql
        // Should have aggregated results
        Assert.True(results.Length > 0)
        for row in results do
            Assert.True(row.ContainsKey("order_count") || row.ContainsKey("COUNT(*)"))

    [<Fact>]
    member _.``Execute having query and verify filtering on aggregates``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn Having.Sql
        // User 1 has 6 orders, which is > 5
        Assert.True(results.Length > 0)

    [<Fact>]
    member _.``Execute order by with limit``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn Limit.Sql
        Assert.True(results.Length <= 10)

    [<Fact>]
    member _.``Execute arithmetic expression query``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn ArithmeticBasic.Sql
        Assert.True(results.Length > 0)
        // Verify the computed column exists
        for row in results do
            Assert.True(row.ContainsKey("total"))

[<Collection("TypeProvider")>]
type TypeProviderRealWorldScenarioTests() =

    [<Fact>]
    member _.``E2E: Query customers and execute against database``() =
        use conn = TestFixtures.createTestDatabase()

        // Use the type provider validated query
        let sql = SelectAll.Sql
        let results = TestFixtures.executeQuery conn sql

        // Verify we got all customers
        Assert.Equal(3, results.Length)

        // Verify customer data
        let names = results |> List.map (fun r -> r.["name"] :?> string) |> Set.ofList
        Assert.Contains("Acme Corp", names)
        Assert.Contains("Tech Corp", names)

    [<Fact>]
    member _.``E2E: Filter active adult users``() =
        use conn = TestFixtures.createTestDatabase()

        // The type provider validates this at compile time
        let sql = FilterComplex.Sql
        let results = TestFixtures.executeQuery conn sql

        // Should only get Alice (age 30, active)
        // Charlie is inactive, Bob and Diana are under 18
        Assert.Equal(1, results.Length)

    [<Fact>]
    member _.``E2E: Join users with orders and calculate totals``() =
        use conn = TestFixtures.createTestDatabase()

        let sql = JoinSimple.Sql
        let results = TestFixtures.executeQuery conn sql

        // Alice has 6 orders, Bob has 1
        Assert.Equal(7, results.Length)

    [<Fact>]
    member _.``E2E: Aggregate order totals by user``() =
        use conn = TestFixtures.createTestDatabase()

        let sql = GroupBy.Sql
        let results = TestFixtures.executeQuery conn sql

        // Should have 2 users with orders (user 1 and user 2)
        Assert.Equal(2, results.Length)

[<Collection("TypeProvider")>]
type TypeProviderQueryPropertyTests() =

    [<Fact>]
    member _.``Query property returns original LQL for all query types``() =
        // Verify each type provider generated type has correct Query property
        Assert.Equal("Customer |> select(*)", SelectAll.Query)
        Assert.Equal("users |> select(users.id, users.name, users.email)", SelectColumns.Query)
        Assert.Equal("users |> filter(fn(row) => row.users.age > 18) |> select(users.name)", FilterSimple.Query)
        Assert.Equal("users |> join(orders, on = users.id = orders.user_id) |> select(users.name, orders.total)", JoinSimple.Query)
        Assert.Equal("orders |> group_by(orders.user_id) |> select(orders.user_id, count(*) as order_count)", GroupBy.Query)

    [<Fact>]
    member _.``Sql property is never null or empty``() =
        Assert.False(String.IsNullOrWhiteSpace(SelectAll.Sql))
        Assert.False(String.IsNullOrWhiteSpace(SelectColumns.Sql))
        Assert.False(String.IsNullOrWhiteSpace(FilterSimple.Sql))
        Assert.False(String.IsNullOrWhiteSpace(JoinSimple.Sql))
        Assert.False(String.IsNullOrWhiteSpace(GroupBy.Sql))
        Assert.False(String.IsNullOrWhiteSpace(OrderBy.Sql))
        Assert.False(String.IsNullOrWhiteSpace(Limit.Sql))
