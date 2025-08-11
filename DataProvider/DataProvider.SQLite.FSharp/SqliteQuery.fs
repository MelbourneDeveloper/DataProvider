namespace DataProvider.SQLite.FSharp

open System
open System.Data
open Microsoft.Data.Sqlite
open Results
open SqliteTypes
open SqliteConnection

/// <summary>
/// Pure functional SQLite query operations
/// </summary>
module SqliteQuery =

    /// <summary>
    /// Converts a data reader row to a result row
    /// </summary>
    let private readRow (reader: SqliteDataReader) =
        let columnCount = reader.FieldCount
        [0..columnCount-1]
        |> List.fold (fun acc i ->
            let name = reader.GetName(i)
            let value = 
                match reader.GetValue(i) with
                | :? DBNull -> null
                | v -> v
            Map.add name value acc) Map.empty

    /// <summary>
    /// Executes a query and returns multiple rows
    /// </summary>
    let executeQuery (config: ConnectionConfig) (query: SqlQuery) =
        withConnection config (fun connection ->
            async {
                match createCommand connection None query with
                | Error error -> return Error error
                | Ok command ->
                    use cmd = command
                    try
                        use reader = cmd.ExecuteReader()
                        let mutable rows = []
                        while reader.Read() do
                            rows <- (readRow reader) :: rows
                        return Ok (List.rev rows)
                    with
                    | ex -> 
                        return Error (SqlError.QueryFailed $"Query execution failed: {ex.Message}")
            })

    /// <summary>
    /// Executes a query and returns the first row or None
    /// </summary>
    let executeQuerySingle (config: ConnectionConfig) (query: SqlQuery) =
        async {
            let! result = executeQuery config query
            return match result with
                   | Ok rows -> 
                       match rows with
                       | head :: _ -> Ok (Some head)
                       | [] -> Ok None
                   | Error error -> Error error
        }

    /// <summary>
    /// Executes a scalar query returning a single value
    /// </summary>
    let executeScalar<'T> (config: ConnectionConfig) (query: SqlQuery) =
        withConnection config (fun connection ->
            async {
                match createCommand connection None query with
                | Error error -> return Error error
                | Ok command ->
                    use cmd = command
                    try
                        let! result = cmd.ExecuteScalarAsync() |> Async.AwaitTask
                        match result with
                        | :? DBNull | null -> return Ok None
                        | value -> 
                            try
                                return Ok (Some (value :?> 'T))
                            with
                            | :? InvalidCastException ->
                                return Error (SqlError.QueryFailed $"Cannot cast result to {typeof<'T>.Name}")
                    with
                    | ex -> 
                        return Error (SqlError.QueryFailed $"Scalar query execution failed: {ex.Message}")
            })

    /// <summary>
    /// Executes a non-query (INSERT, UPDATE, DELETE) and returns affected rows
    /// </summary>
    let executeNonQuery (config: ConnectionConfig) (query: SqlQuery) =
        withConnection config (fun connection ->
            async {
                match createCommand connection None query with
                | Error error -> return Error error
                | Ok command ->
                    use cmd = command
                    try
                        let! affectedRows = cmd.ExecuteNonQueryAsync() |> Async.AwaitTask
                        return Ok affectedRows
                    with
                    | ex -> 
                        return Error (SqlError.QueryFailed $"Non-query execution failed: {ex.Message}")
            })

    /// <summary>
    /// Executes multiple queries in a transaction
    /// </summary>
    let executeBatch (config: ConnectionConfig) (queries: SqlQuery list) (isolationLevel: TransactionLevel option) =
        withTransaction config isolationLevel (fun connection transaction ->
            async {
                let mutable results = []
                let mutable hasError = false
                let mutable lastError = None

                for query in queries do
                    if not hasError then
                        match createCommand connection (Some transaction) query with
                        | Error error -> 
                            hasError <- true
                            lastError <- Some error
                        | Ok command ->
                            use cmd = command
                            try
                                let! affectedRows = cmd.ExecuteNonQueryAsync() |> Async.AwaitTask
                                results <- affectedRows :: results
                            with
                            | ex -> 
                                hasError <- true
                                lastError <- Some (SqlError.QueryFailed $"Batch execution failed: {ex.Message}")

                return match lastError with
                       | Some error -> Error error
                       | None -> Ok (List.rev results)
            })

    /// <summary>
    /// Helper functions for common queries
    /// </summary>
    module Helpers =
        
        /// <summary>
        /// Creates a simple SELECT query
        /// </summary>
        let selectFrom table whereClause parameters =
            let sql = 
                match whereClause with
                | Some where -> $"SELECT * FROM {table} WHERE {where}"
                | None -> $"SELECT * FROM {table}"
            createQueryWithParams sql parameters

        /// <summary>
        /// Creates a parameterized SELECT query
        /// </summary>
        let selectColumns columns table whereClause parameters =
            let columnList = String.concat ", " columns
            let sql = 
                match whereClause with
                | Some where -> $"SELECT {columnList} FROM {table} WHERE {where}"
                | None -> $"SELECT {columnList} FROM {table}"
            createQueryWithParams sql parameters

        /// <summary>
        /// Creates a COUNT query
        /// </summary>
        let count table whereClause parameters =
            let sql = 
                match whereClause with
                | Some where -> $"SELECT COUNT(*) FROM {table} WHERE {where}"
                | None -> $"SELECT COUNT(*) FROM {table}"
            createQueryWithParams sql parameters

        /// <summary>
        /// Creates an EXISTS query
        /// </summary>
        let exists table whereClause parameters =
            let sql = 
                match whereClause with
                | Some where -> $"SELECT EXISTS(SELECT 1 FROM {table} WHERE {where})"
                | None -> $"SELECT EXISTS(SELECT 1 FROM {table})"
            createQueryWithParams sql parameters