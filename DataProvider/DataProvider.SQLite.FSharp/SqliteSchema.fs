namespace DataProvider.SQLite.FSharp

open System
open Results
open SqliteTypes
open SqliteQuery

/// <summary>
/// Pure functional SQLite schema inspection
/// </summary>
module SqliteSchema =

    /// <summary>
    /// Gets all table names in the database
    /// </summary>
    let getTables (config: ConnectionConfig) =
        async {
            let query = createQuery "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name"
            let! result = executeQuery config query
            return match result with
                   | Ok rows -> 
                       let tableNames = rows |> List.map (fun row -> row.["name"] :?> string)
                       Ok tableNames
                   | Error error -> Error error
        }

    /// <summary>
    /// Gets column information for a specific table
    /// </summary>
    let getTableColumns (config: ConnectionConfig) (tableName: string) =
        async {
            let query = createQueryWithParams "PRAGMA table_info(@tableName)" [createParameter "@tableName" tableName]
            let! result = executeQuery config query
            return match result with
                   | Ok rows ->
                       let columns = 
                           rows 
                           |> List.map (fun row ->
                               {
                                   Name = row.["name"] :?> string
                                   Type = row.["type"] :?> string
                                   IsNullable = (row.["notnull"] :?> int64) = 0L
                                   IsPrimaryKey = (row.["pk"] :?> int64) > 0L
                                   DefaultValue = 
                                       match row.["dflt_value"] with
                                       | null -> None
                                       | value -> Some (string value)
                               })
                       Ok columns
                   | Error error -> Error error
        }

    /// <summary>
    /// Gets complete table information including columns
    /// </summary>
    let getTableInfo (config: ConnectionConfig) (tableName: string) =
        async {
            let! columnsResult = getTableColumns config tableName
            return match columnsResult with
                   | Ok columns -> 
                       Ok { Name = tableName; Columns = columns; Schema = None }
                   | Error error -> Error error
        }

    /// <summary>
    /// Gets information for all tables in the database
    /// </summary>
    let getAllTablesInfo (config: ConnectionConfig) =
        async {
            let! tablesResult = getTables config
            match tablesResult with
            | Error error -> return Error error
            | Ok tableNames ->
                let tableInfoTasks = tableNames |> List.map (getTableInfo config)
                let! results = Async.Parallel tableInfoTasks
                
                // Collect successes and failures
                let successes, failures = 
                    results 
                    |> Array.toList
                    |> List.partition (function Ok _ -> true | Error _ -> false)
                
                match failures with
                | [] -> 
                    let tableInfos = successes |> List.map (function Ok info -> info | Error _ -> failwith "Impossible")
                    return Ok tableInfos
                | (Error firstError) :: _ -> return Error firstError
                | _ -> return Error (SqlError.QueryFailed "Unexpected schema inspection error")
        }

    /// <summary>
    /// Gets foreign key information for a table
    /// </summary>
    let getForeignKeys (config: ConnectionConfig) (tableName: string) =
        async {
            let query = createQueryWithParams "PRAGMA foreign_key_list(@tableName)" [createParameter "@tableName" tableName]
            let! result = executeQuery config query
            return match result with
                   | Ok rows ->
                       let foreignKeys = 
                           rows 
                           |> List.map (fun row ->
                               {|
                                   Id = row.["id"] :?> int64
                                   Seq = row.["seq"] :?> int64
                                   Table = row.["table"] :?> string
                                   From = row.["from"] :?> string
                                   To = row.["to"] :?> string
                                   OnUpdate = string row.["on_update"]
                                   OnDelete = string row.["on_delete"]
                                   Match = string row.["match"]
                               |})
                       Ok foreignKeys
                   | Error error -> Error error
        }

    /// <summary>
    /// Gets index information for a table
    /// </summary>
    let getTableIndexes (config: ConnectionConfig) (tableName: string) =
        async {
            let query = createQueryWithParams "PRAGMA index_list(@tableName)" [createParameter "@tableName" tableName]
            let! result = executeQuery config query
            return match result with
                   | Ok rows ->
                       let indexes = 
                           rows 
                           |> List.map (fun row ->
                               {|
                                   Seq = row.["seq"] :?> int64
                                   Name = row.["name"] :?> string
                                   Unique = (row.["unique"] :?> int64) = 1L
                                   Origin = row.["origin"] :?> string
                                   Partial = (row.["partial"] :?> int64) = 1L
                               |})
                       Ok indexes
                   | Error error -> Error error
        }

    /// <summary>
    /// Checks if a table exists
    /// </summary>
    let tableExists (config: ConnectionConfig) (tableName: string) =
        async {
            let query = createQueryWithParams "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName" [createParameter "@tableName" tableName]
            let! result = executeScalar<int64> config query
            return match result with
                   | Ok (Some count) -> Ok (count > 0L)
                   | Ok None -> Ok false
                   | Error error -> Error error
        }

    /// <summary>
    /// Gets the database version (user_version pragma)
    /// </summary>
    let getDatabaseVersion (config: ConnectionConfig) =
        async {
            let query = createQuery "PRAGMA user_version"
            let! result = executeScalar<int64> config query
            return match result with
                   | Ok (Some version) -> Ok version
                   | Ok None -> Ok 0L
                   | Error error -> Error error
        }

    /// <summary>
    /// Sets the database version (user_version pragma)
    /// </summary>
    let setDatabaseVersion (config: ConnectionConfig) (version: int64) =
        async {
            let query = createQuery $"PRAGMA user_version = {version}"
            let! result = executeNonQuery config query
            return match result with
                   | Ok _ -> Ok ()
                   | Error error -> Error error
        }