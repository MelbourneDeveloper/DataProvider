open Microsoft.Data.Sqlite
open Lql.TypeProvider.FSharp

[<EntryPoint>]
let main _ =
    let connStr = "Data Source=test.db"
    
    // Setup database with data
    use conn = new SqliteConnection(connStr)
    conn.Open()
    use cmd = new SqliteCommand("DROP TABLE IF EXISTS Customer; CREATE TABLE Customer (Id INTEGER PRIMARY KEY, CustomerName TEXT); INSERT INTO Customer VALUES (1, 'Acme Corp'), (2, 'Tech Corp');", conn)
    cmd.ExecuteNonQuery() |> ignore
    
    // Execute LQL query and load data using the C# libraries directly
    let lqlResult = LqlCompileTimeChecker.convertToSql "Customer |> select(*)"
    match lqlResult with
    | Ok sql ->
        use sqlCmd = new SqliteCommand(sql, conn)
        use reader = sqlCmd.ExecuteReader()
        let customers = ResizeArray<_>()
        while reader.Read() do
            customers.Add(Map.ofList [
                "Id", box (reader.["Id"])
                "CustomerName", box (reader.["CustomerName"])
            ])
        let data = List.ofSeq customers
        printfn "Loaded %d customers:" (List.length data)
        for customer in data do
            printfn "- ID: %A, Name: %A" customer.["Id"] customer.["CustomerName"]
    | Error err -> 
        printfn "Error: %s" err
    
    0