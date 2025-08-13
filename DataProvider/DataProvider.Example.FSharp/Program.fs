open Microsoft.Data.Sqlite
open LqlValidator

[<EntryPoint>]
let main _ =
    let connStr = "Data Source=test.db"
    
    // Setup database with data
    use conn = new SqliteConnection(connStr)
    conn.Open()
    use cmd = new SqliteCommand("DROP TABLE IF EXISTS Customer; CREATE TABLE Customer (Id INTEGER PRIMARY KEY, CustomerName TEXT); INSERT INTO Customer VALUES (1, 'Acme Corp'), (2, 'Tech Corp');", conn)
    cmd.ExecuteNonQuery() |> ignore
    conn.Close()

    // Execute valid LQL - this will work
    match LqlQuery.Execute(connStr, "Customer |> seflect(*)") with
    | Ok results ->
        printfn "Found %d customers:" results.Length
        for row in results do
            let id = row.["Id"]
            let name = row.["CustomerName"]
            printfn "  ID: %A, Name: %A" id name
    | Error err ->
        printfn "Error: %s" err
    
    // This would cause a compile-time error if we had a true Type Provider
    // For now it will fail at runtime
    match LqlQuery.Execute(connStr, "Customer |> seldect(*)") with
    | Ok results ->
        printfn "This shouldn't happen - invalid LQL should fail"
    | Error err ->
        printfn "Expected error for invalid LQL: %s" err
    
    0