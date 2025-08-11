namespace DataProvider.SQLite.FSharp

open System
open System.Data
open Microsoft.Data.Sqlite
open Results

/// <summary>
/// Simple, functional F# SQLite operations
/// </summary>
module SimpleSqlite =

    /// <summary>
    /// Database connection configuration
    /// </summary>
    type ConnectionConfig = {
        ConnectionString: string
    }

    /// <summary>
    /// Creates a connection configuration
    /// </summary>
    let createConfig connectionString = { ConnectionString = connectionString }

    /// <summary>
    /// Executes a function with a managed SQLite connection
    /// </summary>
    let withConnection<'T> (config: ConnectionConfig) (operation: SqliteConnection -> 'T) =
        try
            use connection = new SqliteConnection(config.ConnectionString)
            connection.Open()
            Ok (operation connection)
        with
        | ex -> Error (SqlError.Create $"Connection failed: {ex.Message}")

    /// <summary>
    /// Executes a function with a managed SQLite connection (async)
    /// </summary>
    let withConnectionAsync<'T> (config: ConnectionConfig) (operation: SqliteConnection -> Async<'T>) =
        async {
            try
                use connection = new SqliteConnection(config.ConnectionString)
                do! connection.OpenAsync() |> Async.AwaitTask
                let! result = operation connection
                return Ok result
            with
            | ex -> return Error (SqlError.Create $"Connection failed: {ex.Message}")
        }

    /// <summary>
    /// Executes a SQL query and returns rows as Map list
    /// </summary>
    let executeQuery (config: ConnectionConfig) (sql: string) (parameters: (string * obj) list) =
        withConnection config (fun connection ->
            use command = new SqliteCommand(sql, connection)
            
            // Add parameters
            parameters |> List.iter (fun (name, value) ->
                let param = command.CreateParameter()
                param.ParameterName <- name
                param.Value <- match value with null -> box DBNull.Value | v -> v
                command.Parameters.Add(param) |> ignore)
            
            use reader = command.ExecuteReader()
            let mutable rows = []
            
            while reader.Read() do
                let columnCount = reader.FieldCount
                let row = 
                    [0..columnCount-1]
                    |> List.fold (fun acc i ->
                        let name = reader.GetName(i)
                        let value = 
                            match reader.GetValue(i) with
                            | :? DBNull -> null
                            | v -> v
                        Map.add name value acc) Map.empty
                rows <- row :: rows
            
            List.rev rows)

    /// <summary>
    /// Executes a SQL query and returns the first row or None
    /// </summary>
    let executeQuerySingle (config: ConnectionConfig) (sql: string) (parameters: (string * obj) list) =
        match executeQuery config sql parameters with
        | Ok rows -> 
            match rows with
            | head :: _ -> Ok (Some head)
            | [] -> Ok None
        | Error err -> Error err

    /// <summary>
    /// Executes a scalar query returning a single value
    /// </summary>
    let executeScalar<'T> (config: ConnectionConfig) (sql: string) (parameters: (string * obj) list) =
        withConnection config (fun connection ->
            use command = new SqliteCommand(sql, connection)
            
            // Add parameters
            parameters |> List.iter (fun (name, value) ->
                let param = command.CreateParameter()
                param.ParameterName <- name
                param.Value <- match value with null -> box DBNull.Value | v -> v
                command.Parameters.Add(param) |> ignore)
            
            let result = command.ExecuteScalar()
            match result with
            | :? DBNull | null -> None
            | value -> Some (value :?> 'T))

    /// <summary>
    /// Executes a non-query (INSERT, UPDATE, DELETE)
    /// </summary>
    let executeNonQuery (config: ConnectionConfig) (sql: string) (parameters: (string * obj) list) =
        withConnection config (fun connection ->
            use command = new SqliteCommand(sql, connection)
            
            // Add parameters
            parameters |> List.iter (fun (name, value) ->
                let param = command.CreateParameter()
                param.ParameterName <- name
                param.Value <- match value with null -> box DBNull.Value | v -> v
                command.Parameters.Add(param) |> ignore)
            
            command.ExecuteNonQuery())

    /// <summary>
    /// Creates a database file if it doesn't exist
    /// </summary>
    let createDatabase (filePath: string) =
        try
            if not (System.IO.File.Exists(filePath)) then
                System.IO.File.Create(filePath).Dispose()
            Ok filePath
        with
        | ex -> Error (SqlError.Create $"Failed to create database: {ex.Message}")

    /// <summary>
    /// Gets all table names in the database
    /// </summary>
    let getTables (config: ConnectionConfig) =
        executeQuery config "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name" []
        |> Result.map (List.map (fun row -> row.["name"] :?> string))

    /// <summary>
    /// Gets column information for a table
    /// </summary>
    let getTableColumns (config: ConnectionConfig) (tableName: string) =
        executeQuery config $"PRAGMA table_info({tableName})" []
        |> Result.map (List.map (fun row ->
            {|
                Name = row.["name"] :?> string
                Type = row.["type"] :?> string
                IsNullable = (row.["notnull"] :?> int64) = 0L
                IsPrimaryKey = (row.["pk"] :?> int64) > 0L
                DefaultValue = match row.["dflt_value"] with null -> None | v -> Some (string v)
            |}))

    /// <summary>
    /// Checks if a table exists
    /// </summary>
    let tableExists (config: ConnectionConfig) (tableName: string) =
        executeScalar<int64> config "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName" ["@tableName", box tableName]
        |> Result.map (function Some count -> count > 0L | None -> false)

    /// <summary>
    /// Simple data insertion
    /// </summary>
    let insertData (config: ConnectionConfig) (table: string) (data: Map<string, obj>) =
        let columns = data |> Map.keys |> String.concat ", "
        let paramNames = data |> Map.keys |> Seq.map (sprintf "@%s") |> String.concat ", "
        let parameters = data |> Map.toList |> List.map (fun (k, v) -> $"@{k}", v)
        let sql = $"INSERT INTO {table} ({columns}) VALUES ({paramNames}); SELECT last_insert_rowid();"
        
        executeScalar<int64> config sql parameters
        |> Result.bind (function Some id -> Ok id | None -> Error (SqlError.Create "Failed to get inserted ID"))

    /// <summary>
    /// Simple functional query builder
    /// </summary>
    module QueryBuilder =
        
        type Query = {
            Table: string option
            Columns: string list
            Where: string option
            Parameters: (string * obj) list
            OrderBy: string option
            Limit: int option
        }
        
        let empty = {
            Table = None
            Columns = ["*"]
            Where = None
            Parameters = []
            OrderBy = None
            Limit = None
        }
        
        let from table query = { query with Table = Some table }
        
        let select columns query = { query with Columns = columns }
        
        let where condition parameters query = 
            { query with Where = Some condition; Parameters = parameters }
        
        let orderBy order query = { query with OrderBy = Some order }
        
        let limit count query = { query with Limit = Some count }
        
        let build query =
            match query.Table with
            | None -> Error (SqlError.Create "No table specified")
            | Some table ->
                let columnList = String.concat ", " query.Columns
                let whereClause = match query.Where with Some w -> $" WHERE {w}" | None -> ""
                let orderClause = match query.OrderBy with Some o -> $" ORDER BY {o}" | None -> ""
                let limitClause = match query.Limit with Some l -> $" LIMIT {l}" | None -> ""
                
                let sql = $"SELECT {columnList} FROM {table}{whereClause}{orderClause}{limitClause}"
                Ok (sql, query.Parameters)
        
        let execute (config: ConnectionConfig) query =
            match build query with
            | Ok (sql, parameters) -> executeQuery config sql parameters
            | Error err -> Error err