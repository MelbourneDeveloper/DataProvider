namespace Lql.TypeProvider.FSharp

open System
open System.IO
open Microsoft.Data.Sqlite
open Lql
open Lql.SQLite

/// <summary>
/// Extension module for working with LQL queries in F#
/// </summary>
[<AutoOpen>]
module LqlExtensions =
    
    /// <summary>
    /// Execute an LQL query against a SQLite database using proper Result handling
    /// </summary>
    /// <param name="connectionString">The SQLite connection string</param>
    /// <param name="lqlQuery">The LQL query string</param>
    let executeLqlQuery (connectionString: string) (lqlQuery: string) =
        async {
            try
                use connection = new SqliteConnection(connectionString)
                do! connection.OpenAsync() |> Async.AwaitTask
                
                match LqlCompileTimeChecker.convertToSql lqlQuery with
                | Ok sql ->
                    use command = new SqliteCommand(sql, connection)
                    use reader = command.ExecuteReader()
                    
                    let results = ResizeArray<Map<string, obj>>()
                    while reader.Read() do
                        let row = Map.ofList [
                            for i in 0 .. reader.FieldCount - 1 ->
                                let columnName = reader.GetName(i)
                                let value = if reader.IsDBNull(i) then box null else reader.GetValue(i)
                                columnName, value
                        ]
                        results.Add(row)
                    
                    return Ok(results |> List.ofSeq)
                | Error errorMessage ->
                    return Error errorMessage
                    
            with ex ->
                return Error($"Database connection exception: {ex.Message}")
        }
    
    /// <summary>
    /// Execute an LQL query synchronously against a SQLite database
    /// </summary>
    /// <param name="connectionString">The SQLite connection string</param>
    /// <param name="lqlQuery">The LQL query string</param>
    let executeLqlQuerySync (connectionString: string) (lqlQuery: string) =
        try
            use connection = new SqliteConnection(connectionString)
            connection.Open()
            
            match LqlCompileTimeChecker.convertToSql lqlQuery with
            | Ok sql ->
                use command = new SqliteCommand(sql, connection)
                use reader = command.ExecuteReader()
                
                let results = ResizeArray<Map<string, obj>>()
                while reader.Read() do
                    let row = Map.ofList [
                        for i in 0 .. reader.FieldCount - 1 ->
                            let columnName = reader.GetName(i)
                            let value = if reader.IsDBNull(i) then box null else reader.GetValue(i)
                            columnName, value
                    ]
                    results.Add(row)
                
                Ok(results |> List.ofSeq)
            | Error errorMessage ->
                Error errorMessage
                
        with ex ->
            Error($"Database connection exception: {ex.Message}")

    /// <summary>
    /// Execute an LQL file against a SQLite database
    /// </summary>
    /// <param name="connectionString">The SQLite connection string</param>
    /// <param name="lqlFilePath">The path to the LQL file</param>
    let executeLqlFile (connectionString: string) (lqlFilePath: string) =
        async {
            let lqlContent = File.ReadAllText(lqlFilePath)
            return! executeLqlQuery connectionString lqlContent
        }

    /// <summary>
    /// Execute an LQL file synchronously against a SQLite database
    /// </summary>
    /// <param name="connectionString">The SQLite connection string</param>
    /// <param name="lqlFilePath">The path to the LQL file</param>
    let executeLqlFileSync (connectionString: string) (lqlFilePath: string) =
        let lqlContent = File.ReadAllText(lqlFilePath)
        executeLqlQuerySync connectionString lqlContent

    /// <summary>
    /// Convert LQL query to SQL without executing
    /// </summary>
    /// <param name="lqlQuery">The LQL query string</param>
    let lqlToSql (lqlQuery: string) = LqlCompileTimeChecker.convertToSql lqlQuery

/// <summary>
/// LQL utilities for F# projects
/// </summary>
module LqlUtils =
    
    /// <summary>
    /// Validate an LQL query without executing it
    /// </summary>
    /// <param name="lqlQuery">The LQL query string</param>
    let validateLql (lqlQuery: string) =
        match LqlCompileTimeChecker.validateLqlSyntax lqlQuery with
        | None -> Ok "LQL query is valid"
        | Some errorMessage -> Error errorMessage

    /// <summary>
    /// Get all .lql files in a directory
    /// </summary>
    /// <param name="directoryPath">The directory to search</param>
    let findLqlFiles (directoryPath: string) =
        Directory.GetFiles(directoryPath, "*.lql", SearchOption.AllDirectories)
        |> Array.toList

    /// <summary>
    /// Execute all .lql files in a directory
    /// </summary>
    /// <param name="connectionString">The SQLite connection string</param>
    /// <param name="directoryPath">The directory containing .lql files</param>
    let executeAllLqlFiles (connectionString: string) (directoryPath: string) =
        async {
            let lqlFiles = findLqlFiles directoryPath
            let results = ResizeArray<string * Result<Map<string, obj> list, string>>()
            
            for lqlFile in lqlFiles do
                let fileName = Path.GetFileNameWithoutExtension(lqlFile) |> Option.ofObj |> Option.defaultValue ""
                let! result = executeLqlFile connectionString lqlFile
                results.Add((fileName, result))
            
            return results |> List.ofSeq
        }