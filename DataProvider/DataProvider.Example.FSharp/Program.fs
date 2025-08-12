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
    
    // FETCH the data with this lql command
    let mapCustomerRow (reader: SqliteDataReader) =
        Map.ofList [
            for i in 0 .. reader.FieldCount - 1 ->
                let columnName = reader.GetName(i)
                let value = if reader.IsDBNull(i) then box null else reader.GetValue(i)
                columnName, value
        ]
    
    let lqlResult = CompileTimeLql.execute conn "Customer |> seldect(*)" mapCustomerRow
    match lqlResult with
    | Ok (data: Map<string, obj> list) ->
        printfn "Found %d customers:" data.Length
        for customer in data do
            let id = customer.["Id"]
            let name = customer.["CustomerName"]
            printfn "  ID: %A, Name: %A" id name
    | Error err -> 
        printfn "Error: %s" err
    
    0