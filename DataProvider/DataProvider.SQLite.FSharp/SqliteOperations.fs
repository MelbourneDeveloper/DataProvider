namespace DataProvider.SQLite.FSharp

open System
open System.IO
open Results
open SqliteTypes
open SqliteConnection
open SqliteQuery
open SqliteSchema

/// <summary>
/// High-level SQLite operations using pure functional programming
/// </summary>
module SqliteOperations =

    /// <summary>
    /// Database initialization and setup operations
    /// </summary>
    module Setup =

        /// <summary>
        /// Creates a new SQLite database file if it doesn't exist
        /// </summary>
        let createDatabase (filePath: string) =
            try
                let directory = Path.GetDirectoryName(filePath)
                if not (Directory.Exists(directory)) then
                    Directory.CreateDirectory(directory) |> ignore
                
                if not (File.Exists(filePath)) then
                    File.Create(filePath).Dispose()
                
                Ok filePath
            with
            | ex -> Error (SqlError.DatabaseConnectionFailed $"Failed to create database: {ex.Message}")

        /// <summary>
        /// Initializes database with schema from SQL script
        /// </summary>
        let initializeSchema (config: ConnectionConfig) (schemaScript: string) =
            async {
                let statements = 
                    schemaScript.Split([|';'|], StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun s -> s.Trim())
                    |> Array.filter (fun s -> not (String.IsNullOrEmpty s))
                    |> Array.map createQuery
                    |> Array.toList

                let! result = executeBatch config statements (Some ReadCommitted)
                return match result with
                       | Ok _ -> Ok "Schema initialized successfully"
                       | Error error -> Error error
            }

        /// <summary>
        /// Runs database migrations
        /// </summary>
        let runMigrations (config: ConnectionConfig) (migrations: (int64 * string) list) =
            async {
                let! currentVersionResult = getDatabaseVersion config
                match currentVersionResult with
                | Error error -> return Error error
                | Ok currentVersion ->
                    let pendingMigrations = 
                        migrations 
                        |> List.filter (fun (version, _) -> version > currentVersion)
                        |> List.sortBy fst

                    if List.isEmpty pendingMigrations then
                        return Ok "No pending migrations"
                    else
                        let mutable latestVersion = currentVersion
                        let mutable hasError = false
                        let mutable lastError = None

                        for (version, script) in pendingMigrations do
                            if not hasError then
                                let! migrationResult = initializeSchema config script
                                match migrationResult with
                                | Ok _ -> 
                                    let! versionResult = setDatabaseVersion config version
                                    match versionResult with
                                    | Ok _ -> latestVersion <- version
                                    | Error error -> 
                                        hasError <- true
                                        lastError <- Some error
                                | Error error ->
                                    hasError <- true
                                    lastError <- Some error

                        return match lastError with
                               | Some error -> Error error
                               | None -> Ok $"Migrated to version {latestVersion}"
            }

    /// <summary>
    /// Data access operations
    /// </summary>
    module Data =

        /// <summary>
        /// Inserts a single record and returns the new ID
        /// </summary>
        let insert<'T> (config: ConnectionConfig) (table: string) (data: Map<string, obj>) =
            async {
                let columns = data |> Map.keys |> String.concat ", "
                let paramNames = data |> Map.keys |> Seq.map (sprintf "@%s") |> String.concat ", "
                let parameters = data |> Map.toList |> List.map (fun (k, v) -> createParameter $"@{k}" v)
                
                let query = createQueryWithParams $"INSERT INTO {table} ({columns}) VALUES ({paramNames}); SELECT last_insert_rowid();" parameters

                let! result = executeScalar<int64> config query
                return match result with
                       | Ok (Some id) -> Ok id
                       | Ok None -> Error (SqlError.QueryFailed "Failed to get inserted ID")
                       | Error error -> Error error
            }

        /// <summary>
        /// Updates records and returns affected count
        /// </summary>
        let update (config: ConnectionConfig) (table: string) (data: Map<string, obj>) (whereClause: string) (whereParams: SqlParameter list) =
            async {
                let setClause = 
                    data 
                    |> Map.keys 
                    |> Seq.map (sprintf "%s = @%s") 
                    |> String.concat ", "

                let dataParams = data |> Map.toList |> List.map (fun (k, v) -> createParameter $"@{k}" v)
                let allParams = List.append dataParams whereParams

                let query = createQueryWithParams 
                    $"UPDATE {table} SET {setClause} WHERE {whereClause}"
                    allParams

                return! executeNonQuery config query
            }

        /// <summary>
        /// Deletes records and returns affected count
        /// </summary>
        let delete (config: ConnectionConfig) (table: string) (whereClause: string) (whereParams: SqlParameter list) =
            async {
                let query = createQueryWithParams 
                    $"DELETE FROM {table} WHERE {whereClause}"
                    whereParams

                return! executeNonQuery config query
            }

        /// <summary>
        /// Performs an upsert (INSERT OR REPLACE)
        /// </summary>
        let upsert (config: ConnectionConfig) (table: string) (data: Map<string, obj>) =
            async {
                let columns = data |> Map.keys |> String.concat ", "
                let paramNames = data |> Map.keys |> Seq.map (sprintf "@%s") |> String.concat ", "
                let parameters = data |> Map.toList |> List.map (fun (k, v) -> createParameter $"@{k}" v)
                
                let query = createQueryWithParams 
                    $"INSERT OR REPLACE INTO {table} ({columns}) VALUES ({paramNames})"
                    parameters

                return! executeNonQuery config query
            }

    /// <summary>
    /// Bulk operations for performance
    /// </summary>
    module Bulk =

        /// <summary>
        /// Inserts multiple records in a transaction
        /// </summary>
        let insertMany (config: ConnectionConfig) (table: string) (records: Map<string, obj> list) =
            async {
                match records with
                | [] -> return Ok []
                | firstRecord :: _ ->
                    let columns = firstRecord |> Map.keys |> String.concat ", "
                    let paramNames = firstRecord |> Map.keys |> Seq.map (sprintf "@%s") |> String.concat ", "
                    
                    let queries = 
                        records
                        |> List.map (fun record ->
                            let parameters = record |> Map.toList |> List.map (fun (k, v) -> createParameter $"@{k}" v)
                            createQueryWithParams $"INSERT INTO {table} ({columns}) VALUES ({paramNames})" parameters)

                    let! result = executeBatch config queries (Some ReadCommitted)
                    return match result with
                           | Ok affectedCounts -> Ok affectedCounts
                           | Error error -> Error error
            }

        /// <summary>
        /// Copies data from one table to another
        /// </summary>
        let copyTable (config: ConnectionConfig) (sourceTable: string) (targetTable: string) (whereClause: string option) =
            async {
                let sql = 
                    match whereClause with
                    | Some where -> $"INSERT INTO {targetTable} SELECT * FROM {sourceTable} WHERE {where}"
                    | None -> $"INSERT INTO {targetTable} SELECT * FROM {sourceTable}"
                
                let query = createQuery sql
                return! executeNonQuery config query
            }

    /// <summary>
    /// Pipeline-style query building
    /// </summary>
    module Pipeline =

        /// <summary>
        /// Query builder type for fluent API
        /// </summary>
        type QueryBuilder = {
            Table: string option
            Columns: string list
            Joins: string list
            Conditions: string list
            GroupBy: string list
            Having: string list
            OrderBy: string list
            Limit: int option
            Parameters: SqlParameter list
        }

        /// <summary>
        /// Creates an empty query builder
        /// </summary>
        let empty = {
            Table = None
            Columns = ["*"]
            Joins = []
            Conditions = []
            GroupBy = []
            Having = []
            OrderBy = []
            Limit = None
            Parameters = []
        }

        /// <summary>
        /// Sets the table to query from
        /// </summary>
        let from table builder = { builder with Table = Some table }

        /// <summary>
        /// Adds columns to select
        /// </summary>
        let select columns builder = { builder with Columns = columns }

        /// <summary>
        /// Adds a WHERE condition
        /// </summary>
        let where condition parameters builder = 
            { builder with 
                Conditions = condition :: builder.Conditions
                Parameters = List.append parameters builder.Parameters }

        /// <summary>
        /// Adds a JOIN clause
        /// </summary>
        let join joinClause builder = { builder with Joins = joinClause :: builder.Joins }

        /// <summary>
        /// Adds ORDER BY clause
        /// </summary>
        let orderBy orderClause builder = { builder with OrderBy = orderClause :: builder.OrderBy }

        /// <summary>
        /// Adds LIMIT clause
        /// </summary>
        let limit count builder = { builder with Limit = Some count }

        /// <summary>
        /// Builds the final SQL query
        /// </summary>
        let build builder =
            match builder.Table with
            | None -> Error (SqlError.QueryFailed "Table not specified")
            | Some table ->
                let columnList = String.concat ", " builder.Columns
                let joins = String.concat " " (List.rev builder.Joins)
                let conditions = 
                    match List.rev builder.Conditions with
                    | [] -> ""
                    | conds -> "WHERE " + String.concat " AND " conds
                let ordering = 
                    match List.rev builder.OrderBy with
                    | [] -> ""
                    | orders -> "ORDER BY " + String.concat ", " orders
                let limiting = 
                    match builder.Limit with
                    | Some count -> $"LIMIT {count}"
                    | None -> ""

                let sql = [
                    $"SELECT {columnList} FROM {table}"
                    joins
                    conditions
                    ordering
                    limiting
                ] |> List.filter (fun s -> not (String.IsNullOrWhiteSpace s))
                  |> String.concat " "

                Ok (createQueryWithParams sql builder.Parameters)

        /// <summary>
        /// Executes the built query
        /// </summary>
        let execute (config: ConnectionConfig) builder =
            async {
                match build builder with
                | Error error -> return Error error
                | Ok query -> return! executeQuery config query
            }