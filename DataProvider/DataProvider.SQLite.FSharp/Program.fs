open System
open System.IO
open DataProvider.SQLite.FSharp.SimpleSqlite

/// <summary>
/// Demonstration of the simple F# SQLite library
/// </summary>
[<EntryPoint>]
let main argv =
    
    let databasePath = Path.Combine(__SOURCE_DIRECTORY__, "test.db")
    let config = createConfig $"Data Source={databasePath}"
    
    printfn "🚀 F# SQLite Functional Programming Demo"
    printfn "========================================"
    
    try
        // Step 1: Create database
        match createDatabase databasePath with
        | Error error -> 
            printfn "❌ Failed to create database: %A" error
            1
        | Ok _ ->
            printfn "✅ Database created: %s" databasePath
            
            // Step 2: Create schema
            let createSchema () =
                let customerTable = """
                    CREATE TABLE IF NOT EXISTS Customer (
                        Id INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Email TEXT,
                        CreatedDate TEXT NOT NULL
                    )
                """
                
                let orderTable = """
                    CREATE TABLE IF NOT EXISTS [Order] (
                        Id INTEGER PRIMARY KEY,
                        CustomerId INTEGER NOT NULL,
                        OrderNumber TEXT NOT NULL,
                        OrderDate TEXT NOT NULL,
                        Total REAL NOT NULL,
                        FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
                    )
                """
                
                match executeNonQuery config customerTable [], executeNonQuery config orderTable [] with
                | Ok _, Ok _ -> Ok "Schema created"
                | Error err, _ | _, Error err -> Error err
            
            match createSchema () with
            | Error error ->
                printfn "❌ Failed to create schema: %A" error
                1
            | Ok _ ->
                printfn "✅ Database schema created"
                
                // Step 3: Insert sample data
                let insertCustomers () =
                    let customers = [
                        Map.ofList [("Name", box "Acme Corp"); ("Email", box "contact@acme.com"); ("CreatedDate", box "2024-01-01")]
                        Map.ofList [("Name", box "Tech Solutions"); ("Email", box "info@tech.com"); ("CreatedDate", box "2024-01-02")]
                        Map.ofList [("Name", box "Global Industries"); ("Email", box "hello@global.com"); ("CreatedDate", box "2024-01-03")]
                    ]
                    
                    customers
                    |> List.map (insertData config "Customer")
                    |> List.choose (function Ok id -> Some id | Error _ -> None)
                
                let customerIds = insertCustomers ()
                printfn "✅ Inserted %d customers with IDs: %A" customerIds.Length customerIds
                
                // Step 4: Query data using functional approach
                let queryCustomers () =
                    QueryBuilder.empty
                    |> QueryBuilder.from "Customer"
                    |> QueryBuilder.select ["Id"; "Name"; "Email"]
                    |> QueryBuilder.where "Name LIKE @pattern" ["@pattern", box "%Corp%"]
                    |> QueryBuilder.orderBy "Name"
                    |> QueryBuilder.execute config
                
                match queryCustomers () with
                | Error error ->
                    printfn "❌ Query failed: %A" error
                    1
                | Ok results ->
                    printfn "✅ Found %d matching customers:" results.Length
                    results |> List.iter (fun row ->
                        let id = row.["Id"] :?> int64
                        let name = row.["Name"] :?> string
                        let email = match row.["Email"] with null -> "N/A" | v -> string v
                        printfn "   - ID: %d, Name: %s, Email: %s" id name email)
                    
                    // Step 5: Insert orders
                    if not customerIds.IsEmpty then
                        let firstCustomerId = customerIds.Head
                        let orderData = Map.ofList [
                            ("CustomerId", box firstCustomerId)
                            ("OrderNumber", box "ORD-001")
                            ("OrderDate", box "2024-01-15")
                            ("Total", box 1250.50)
                        ]
                        
                        match insertData config "[Order]" orderData with
                        | Error error -> 
                            printfn "❌ Failed to insert order: %A" error
                            1
                        | Ok orderId ->
                            printfn "✅ Inserted order with ID: %d" orderId
                            
                            // Step 6: Join query
                            let joinQuery = """
                                SELECT c.Name as CustomerName, o.OrderNumber, o.Total 
                                FROM Customer c 
                                JOIN [Order] o ON c.Id = o.CustomerId
                                ORDER BY c.Name
                            """
                            
                            match executeQuery config joinQuery [] with
                            | Error error ->
                                printfn "❌ Join query failed: %A" error
                                1
                            | Ok joinResults ->
                                printfn "✅ Join query results:"
                                joinResults |> List.iter (fun row ->
                                    let customerName = row.["CustomerName"] :?> string
                                    let orderNumber = row.["OrderNumber"] :?> string
                                    let total = row.["Total"] :?> float
                                    printfn "   - %s ordered %s for $%.2f" customerName orderNumber total)
                                
                                // Step 7: Schema inspection
                                match getTables config with
                                | Error error ->
                                    printfn "❌ Failed to get tables: %A" error
                                    1
                                | Ok tables ->
                                    printfn "✅ Database tables: %s" (String.concat ", " tables)
                                    
                                    // Check table structure
                                    match getTableColumns config "Customer" with
                                    | Error error ->
                                        printfn "❌ Failed to get Customer columns: %A" error
                                        1
                                    | Ok columns ->
                                        printfn "✅ Customer table structure:"
                                        columns |> List.iter (fun col ->
                                            let nullable = if col.IsNullable then "NULL" else "NOT NULL"
                                            let pk = if col.IsPrimaryKey then " [PK]" else ""
                                            printfn "   - %s: %s %s%s" col.Name col.Type nullable pk)
                                        
                                        // Final success message
                                        printfn ""
                                        printfn "🎉 F# SQLite Demo Completed Successfully!"
                                        printfn ""
                                        printfn "✨ Features Demonstrated:"
                                        printfn "   🔹 Pure functional F# programming"
                                        printfn "   🔹 Result type for error handling"
                                        printfn "   🔹 Automatic resource management with 'use'"
                                        printfn "   🔹 Functional query builder with pipeline style"
                                        printfn "   🔹 Schema inspection and metadata"
                                        printfn "   🔹 Type-safe parameter binding"
                                        printfn "   🔹 Clean separation of concerns"
                                        printfn "   🔹 No imperative C# patterns!"
                                        printfn ""
                                        0
                    else
                        printfn "⚠️ No customers inserted"
                        1
                        
    with
    | ex ->
        printfn "💥 Unexpected error: %s" ex.Message
        printfn "Stack trace: %s" ex.StackTrace
        1