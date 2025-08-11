namespace DataProvider.SQLite.FSharp

open System
open Microsoft.Data.Sqlite
open Results
open SqliteTypes

/// <summary>
/// Pure functional SQLite connection management
/// </summary>
module SqliteConnection =

    /// <summary>
    /// Creates and opens a SQLite connection
    /// </summary>
    let private createConnection (config: ConnectionConfig) = 
        async {
            try
                let connection = new SqliteConnection(config.ConnectionString)
                match config.Timeout with
                | Some timeout -> connection.DefaultTimeout <- int timeout.TotalSeconds
                | None -> ()
                
                do! connection.OpenAsync() |> Async.AwaitTask
                return Ok connection
            with
            | ex -> return Error (SqlError.DatabaseConnectionFailed $"Failed to connect to SQLite: {ex.Message}")
        }

    /// <summary>
    /// Executes a function with a managed connection
    /// </summary>
    let withConnection<'T> (config: ConnectionConfig) (operation: SqliteConnection -> Async<Result<'T, SqlError>>) =
        async {
            let! connectionResult = createConnection config
            match connectionResult with
            | Ok connection ->
                use conn = connection
                return! operation conn
            | Error error -> return Error error
        }

    /// <summary>
    /// Executes a function within a transaction
    /// </summary>
    let withTransaction<'T> 
        (config: ConnectionConfig) 
        (isolationLevel: TransactionLevel option) 
        (operation: SqliteConnection -> SqliteTransaction -> Async<Result<'T, SqlError>>) =
        
        let mapIsolationLevel = function
            | ReadUncommitted -> System.Data.IsolationLevel.ReadUncommitted
            | ReadCommitted -> System.Data.IsolationLevel.ReadCommitted
            | RepeatableRead -> System.Data.IsolationLevel.RepeatableRead
            | Serializable -> System.Data.IsolationLevel.Serializable

        withConnection config (fun connection ->
            async {
                let isolation = isolationLevel |> Option.map mapIsolationLevel
                let transaction = 
                    match isolation with
                    | Some level -> connection.BeginTransaction(level)
                    | None -> connection.BeginTransaction()
                
                use txn = transaction
                try
                    let! result = operation connection txn
                    match result with
                    | Ok value -> 
                        do! txn.CommitAsync() |> Async.AwaitTask
                        return Ok value
                    | Error error ->
                        do! txn.RollbackAsync() |> Async.AwaitTask
                        return Error error
                with
                | ex ->
                    try
                        do! txn.RollbackAsync() |> Async.AwaitTask
                    with
                    | _ -> () // Ignore rollback errors
                    return Error (SqlError.DatabaseTransactionFailed $"Transaction failed: {ex.Message}")
            })

    /// <summary>
    /// Creates a command with parameters
    /// </summary>
    let createCommand (connection: SqliteConnection) (transaction: SqliteTransaction option) (query: SqlQuery) =
        try
            let command = new SqliteCommand(query.Statement, connection)
            
            match transaction with
            | Some txn -> command.Transaction <- txn
            | None -> ()
            
            // Add parameters
            query.Parameters
            |> List.iter (fun param ->
                let sqlParam = command.CreateParameter()
                sqlParam.ParameterName <- param.Name
                sqlParam.Value <- match param.Value with null -> box DBNull.Value | v -> v
                match param.DbType with
                | Some dbType -> sqlParam.DbType <- dbType
                | None -> ()
                command.Parameters.Add(sqlParam) |> ignore)
            
            Ok command
        with
        | ex -> Error (SqlError.QueryFailed $"Failed to create command: {ex.Message}")

    /// <summary>
    /// Tests if a connection string is valid
    /// </summary>
    let testConnection (config: ConnectionConfig) =
        async {
            let! result = withConnection config (fun _ -> async { return Ok () })
            return match result with
                   | Ok () -> Ok "Connection successful"
                   | Error error -> Error error
        }