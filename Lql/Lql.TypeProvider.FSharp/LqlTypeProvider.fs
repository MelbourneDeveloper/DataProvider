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
    /// Execute an LQL query against a SQLite database using exception-based error handling
    /// </summary>
    /// <param name="connectionString">The SQLite connection string</param>
    /// <param name="lqlQuery">The LQL query string</param>
    let executeLqlQuery (connectionString: string) (lqlQuery: string) =
        async {
            try
                use connection = new SqliteConnection(connectionString)
                do! connection.OpenAsync() |> Async.AwaitTask
                
                let lqlStatement = LqlStatementConverter.ToStatement(lqlQuery)
                
                // Handle the Result type from the library
                if lqlStatement.GetType().Name.Contains("Success") then
                    let statement = lqlStatement.GetType().GetProperty("Value").GetValue(lqlStatement) :?> LqlStatement
                    let sqlResult = statement.ToSQLite()
                    
                    if sqlResult.GetType().Name.Contains("Success") then
                        let sql = sqlResult.GetType().GetProperty("Value").GetValue(sqlResult) :?> string
                        
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
                    else
                        let errorValue = sqlResult.GetType().GetProperty("ErrorValue").GetValue(sqlResult)
                        let message = errorValue.GetType().GetProperty("Message").GetValue(errorValue) :?> string
                        return Error($"SQL conversion error: {message}")
                else
                    let errorValue = lqlStatement.GetType().GetProperty("ErrorValue").GetValue(lqlStatement)
                    let message = errorValue.GetType().GetProperty("Message").GetValue(errorValue) :?> string
                    return Error($"Parse error: {message}")
                    
            with ex ->
                return Error($"Exception: {ex.Message}")
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
            
            let lqlStatement = LqlStatementConverter.ToStatement(lqlQuery)
            
            if lqlStatement.GetType().Name.Contains("Success") then
                let statement = lqlStatement.GetType().GetProperty("Value").GetValue(lqlStatement) :?> LqlStatement
                let sqlResult = statement.ToSQLite()
                
                if sqlResult.GetType().Name.Contains("Success") then
                    let sql = sqlResult.GetType().GetProperty("Value").GetValue(sqlResult) :?> string
                    
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
                else
                    let errorValue = sqlResult.GetType().GetProperty("ErrorValue").GetValue(sqlResult)
                    let message = errorValue.GetType().GetProperty("Message").GetValue(errorValue) :?> string
                    Error($"SQL conversion error: {message}")
            else
                let errorValue = lqlStatement.GetType().GetProperty("ErrorValue").GetValue(lqlStatement)
                let message = errorValue.GetType().GetProperty("Message").GetValue(errorValue) :?> string
                Error($"Parse error: {message}")
                
        with ex ->
            Error($"Exception: {ex.Message}")

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
    let lqlToSql (lqlQuery: string) =
        try
            let lqlStatement = LqlStatementConverter.ToStatement(lqlQuery)
            
            if lqlStatement.GetType().Name.Contains("Success") then
                let statement = lqlStatement.GetType().GetProperty("Value").GetValue(lqlStatement) :?> LqlStatement
                let sqlResult = statement.ToSQLite()
                
                if sqlResult.GetType().Name.Contains("Success") then
                    let sql = sqlResult.GetType().GetProperty("Value").GetValue(sqlResult) :?> string
                    Ok sql
                else
                    let errorValue = sqlResult.GetType().GetProperty("ErrorValue").GetValue(sqlResult)
                    let message = errorValue.GetType().GetProperty("Message").GetValue(errorValue) :?> string
                    Error($"SQL conversion error: {message}")
            else
                let errorValue = lqlStatement.GetType().GetProperty("ErrorValue").GetValue(lqlStatement)
                let message = errorValue.GetType().GetProperty("Message").GetValue(errorValue) :?> string
                Error($"Parse error: {message}")
                
        with ex ->
            Error($"Exception: {ex.Message}")

/// <summary>
/// LQL utilities for F# projects
/// </summary>
module LqlUtils =
    
    /// <summary>
    /// Validate an LQL query without executing it
    /// </summary>
    /// <param name="lqlQuery">The LQL query string</param>
    let validateLql (lqlQuery: string) =
        try
            let lqlStatement = LqlStatementConverter.ToStatement(lqlQuery)
            if lqlStatement.GetType().Name.Contains("Success") then
                Ok "LQL query is valid"
            else
                let errorValue = lqlStatement.GetType().GetProperty("ErrorValue").GetValue(lqlStatement)
                let message = errorValue.GetType().GetProperty("Message").GetValue(errorValue) :?> string
                Error($"Parse error: {message}")
        with ex ->
            Error($"Exception: {ex.Message}")

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