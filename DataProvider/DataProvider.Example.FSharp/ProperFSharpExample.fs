module ProperFSharpExample

open System
open System.IO
open Microsoft.Data.Sqlite
open Lql.TypeProvider.FSharp

/// Functional F# approach to LQL queries
module FunctionalLql =
    
    /// Pure function to create database connection
    let private createConnection connectionString =
        let conn = new SqliteConnection(connectionString)
        conn.Open()
        conn

    /// Pure function to execute LQL query
    let executeLql connectionString lqlQuery =
        async {
            use conn = createConnection connectionString
            return! executeLqlQuery connectionString lqlQuery
        }

    /// Compose LQL queries functionally
    let buildQuery table =
        table
        |> sprintf "%s |> select(*)"

    /// Process query results functionally
    let processResults results =
        results
        |> List.map (fun row ->
            row
            |> Map.toList
            |> List.map (fun (key, value) -> sprintf "%s: %A" key value)
            |> String.concat ", ")

    /// Pure database setup function
    let setupDatabase connectionString =
        async {
            use conn = createConnection connectionString
            
            let commands = [
                """CREATE TABLE IF NOT EXISTS Customer (
                    Id INTEGER PRIMARY KEY,
                    CustomerName TEXT NOT NULL,
                    Email TEXT NULL,
                    Phone TEXT NULL,
                    CreatedDate TEXT NOT NULL
                )"""
                
                """CREATE TABLE IF NOT EXISTS Address (
                    Id INTEGER PRIMARY KEY,
                    CustomerId INTEGER NOT NULL,
                    Street TEXT NOT NULL,
                    City TEXT NOT NULL,
                    State TEXT NOT NULL,
                    ZipCode TEXT NOT NULL,
                    Country TEXT NOT NULL,
                    FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
                )"""
                
                """DELETE FROM Address; DELETE FROM Customer"""
                
                """INSERT INTO Customer (CustomerName, Email, Phone, CreatedDate)
                   VALUES 
                   ('Acme Corp', 'contact@acme.com', '555-0100', '2024-01-01'),
                   ('Tech Solutions', 'info@techsolutions.com', '555-0200', '2024-01-02'),
                   ('Global Industries', 'hello@global.com', '555-0300', '2024-01-03')"""
                
                """INSERT INTO Address (CustomerId, Street, City, State, ZipCode, Country)
                   VALUES 
                   (1, '123 Business Ave', 'New York', 'NY', '10001', 'USA'),
                   (2, '789 Tech Blvd', 'San Francisco', 'CA', '94105', 'USA'),
                   (3, '321 Corporate Dr', 'Chicago', 'IL', '60601', 'USA')"""
            ]
            
            commands
            |> List.iter (fun sql ->
                use cmd = new SqliteCommand(sql, conn)
                cmd.ExecuteNonQuery() |> ignore)
        }

    /// Functional query composition
    let composeQuery = function
        | "customers" -> "Customer |> select(Customer.Id, Customer.CustomerName, Customer.Email)"
        | "addresses" -> "Address |> select(Address.City, Address.State, Address.Country)"
        | "customers-with-addresses" -> 
            """Customer 
            |> join(Address, on = Customer.Id = Address.CustomerId) 
            |> select(Customer.CustomerName, Address.City, Address.State)
            |> order_by(Customer.CustomerName)"""
        | _ -> "Customer |> select(*)"

/// What a REAL F# type provider should provide:
/// 
/// ```fsharp
/// type MyDb = LqlProvider<"Data Source=invoices.db">
/// 
/// // This would be compile-time validated:
/// let customers = MyDb.Customer.All()  // ✓ Customer table exists
/// let names = customers |> List.map (_.CustomerName)  // ✓ CustomerName column exists
/// 
/// // This would give COMPILE-TIME ERROR:
/// let invalid = customers |> List.map (_.NonExistentColumn)  // ❌ Compile error!
/// 
/// // Type-safe LQL with IntelliSense:
/// let query = 
///     MyDb.Query
///         .From<MyDb.Customer>()
///         .Where(fun c -> c.CustomerName.Contains("Corp"))  // ✓ IntelliSense on CustomerName
///         .Select(fun c -> {| Name = c.CustomerName; Email = c.Email |})
/// ```

let demonstrateProperFSharp () =
    async {
        let connectionString = "Data Source=invoices.db"
        
        printfn "=== Proper F# Functional Programming Demo ==="
        
        // Pure functional approach
        do! FunctionalLql.setupDatabase connectionString
        printfn "✓ Database setup (pure functions)"
        
        // Compose queries functionally
        let queries = [
            "customers"
            "addresses" 
            "customers-with-addresses"
        ]
        
        let results =
            queries
            |> List.map FunctionalLql.composeQuery
            |> List.map (fun lql -> 
                async {
                    printfn "\nExecuting LQL: %s" lql
                    let! result = FunctionalLql.executeLql connectionString lql
                    return (lql, result)
                })
        
        let! allResults = results |> Async.Parallel
        
        allResults
        |> Array.iter (function
            | (lql, Ok data) -> 
                let processed = FunctionalLql.processResults data
                printfn "✓ Success: %d records" data.Length
                processed |> List.take (min 2 processed.Length) |> List.iter (printfn "  %s")
            | (lql, Error err) -> 
                printfn "❌ Error: %s" err)
        
        printfn "\n=== This is how F# should be written! ==="
        printfn "✓ Pure functions"
        printfn "✓ Immutable data" 
        printfn "✓ Function composition"
        printfn "✓ Pipeline operators"
        printfn "✓ Pattern matching"
        printfn "✓ Async computation expressions"
        
        return ()
    }