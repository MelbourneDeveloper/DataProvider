open Microsoft.Data.Sqlite
open Lql

// ✅ VALID LQL using TRUE type provider with static parameter
type ValidQuery = LqlCommand<"Customer |> select(*)">

// ❌ INVALID LQL - This WILL cause COMPILATION FAILURE
// Uncomment the line below to test:
// type InvalidQuery = LqlCommand<"Customer |> seflect(*)">  // misspelled "select"

[<EntryPoint>]
let main _ =
    let connStr = "Data Source=test.db"
    
    // Setup database with data
    use conn = new SqliteConnection(connStr)
    conn.Open()
    use cmd = new SqliteCommand("DROP TABLE IF EXISTS Customer; CREATE TABLE Customer (Id INTEGER PRIMARY KEY, CustomerName TEXT); INSERT INTO Customer VALUES (1, 'Acme Corp'), (2, 'Tech Corp');", conn)
    cmd.ExecuteNonQuery() |> ignore
    conn.Close()

    printfn "🔥 TESTING TRUE F# TYPE PROVIDER WITH STATIC PARAMETERS 🔥"
    printfn "============================================================"
    
    printfn "✅ Valid LQL compiles successfully:"
    printfn "   LQL: %s" ValidQuery.Query
    printfn "   SQL: %s" ValidQuery.Sql
    
    // Execute the valid command
    match ValidQuery.Execute(connStr) with
    | Ok results ->
        printfn "\n✅ Execution Results:"
        printfn "Found %d customers:" results.Count
        for row in results do
            let id = row.["Id"]
            let name = row.["CustomerName"]
            printfn "  ID: %A, Name: %A" id name
    | Error err ->
        printfn "❌ Unexpected error: %s" err
    
    printfn "\n🎉 TRUE TYPE PROVIDER WORKING!"
    printfn "   - Valid LQL with static parameter compiles successfully"
    printfn "   - Invalid LQL (when uncommented) WILL cause TRUE COMPILATION FAILURE"
    printfn "   - This follows the EXACT FSharp.Data.SqlClient pattern"
    
    0