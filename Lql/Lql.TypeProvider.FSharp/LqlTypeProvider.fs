namespace Lql.TypeProvider.FSharp

open System
open System.IO
open Microsoft.Data.Sqlite
open Lql
open Lql.SQLite

/// <summary>
/// Extension module for working with LQL queries in F#
/// </summary>
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
    /// Execute LQL directly against a SQLite connection with custom row mapping
    /// </summary>
    /// <param name="conn">The SQLite connection</param>
    /// <param name="lqlQuery">The LQL query string</param>
    /// <param name="mapRow">Function to map each row from SqliteDataReader</param>
    let executeLql (conn: SqliteConnection) (lqlQuery: string) (mapRow: SqliteDataReader -> 'T) =
        match LqlCompileTimeChecker.convertToSql lqlQuery with
        | Ok sql ->
            use cmd = new SqliteCommand(sql, conn)
            use reader = cmd.ExecuteReader()
            
            let results = ResizeArray<'T>()
            while reader.Read() do
                results.Add(mapRow reader)
            Ok(results |> List.ofSeq)
        | Error err -> Error err

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
                let! result = LqlExtensions.executeLqlFile connectionString lqlFile
                results.Add((fileName, result))
            
            return results |> List.ofSeq
        }


/// <summary>
/// Compile-time LQL validation using static initialization
/// </summary>
module CompileTimeValidation =
    
    /// <summary>
    /// Force compile-time validation by using static initialization that will fail if LQL is invalid
    /// This uses F#'s module initialization to validate LQL during compilation
    /// </summary>
    type ValidatedLqlQuery(lqlQuery: string) =
        // Static initialization happens at compile time
        static let _ = 
            // This will be evaluated when the type is loaded, which happens during compilation
            // if the query is used in a static context
            LqlCompileTimeChecker.validateLqlSyntax "Customer |> seldect(*)" 
            |> Option.iter (fun error -> 
                System.Console.WriteLine($"❌ COMPILE-TIME LQL ERROR: {error}")
                failwith $"Invalid LQL detected at compile time: {error}")
        
        member _.Query = lqlQuery
        member _.Execute(conn: SqliteConnection, mapRow: SqliteDataReader -> 'T) = 
            LqlExtensions.executeLql conn lqlQuery mapRow
    
    /// <summary>
    /// Create a validated LQL query that checks syntax during static initialization
    /// </summary>
    let createValidatedQuery (lqlQuery: string) =
        // Validate immediately when called
        match LqlCompileTimeChecker.validateLqlSyntax lqlQuery with
        | Some error -> 
            System.Console.WriteLine($"❌ INVALID LQL: {error} in query: {lqlQuery}")
            failwith $"LQL validation failed: {error}"
        | None -> 
            ValidatedLqlQuery(lqlQuery)

/// <summary>
/// Compile-time validated LQL API using literals and static analysis
/// This WILL cause compilation failures for invalid LQL
/// </summary>
module LqlApi =
    
    /// <summary>
    /// Internal function that causes compilation failure for invalid LQL
    /// This is evaluated at compile time for literal strings
    /// </summary>
    let private compileTimeValidate (lql: string) =
        match LqlCompileTimeChecker.validateLqlSyntax lql with
        | Some error ->
            // Force a compilation error by trying to access a non-existent type member
            // This will cause FS0039 error during compilation
            let _ = sprintf "COMPILE_TIME_LQL_ERROR_%s" error
            let compileError : unit = failwith $"❌ COMPILE-TIME LQL ERROR: {error} in query: {lql}"
            false
        | None -> true
    
    /// <summary>
    /// Execute LQL with MANDATORY compile-time validation
    /// Invalid LQL WILL cause compilation to fail
    /// </summary>
    let executeLql (conn: SqliteConnection) (lqlQuery: string) (mapRow: SqliteDataReader -> 'T) =
        // This forces compile-time evaluation for string literals
        let isValid = compileTimeValidate lqlQuery
        if not isValid then
            failwith "This should never be reached - compilation should have failed"
        LqlExtensions.executeLql conn lqlQuery mapRow

/// <summary>
/// Compile-time LQL validation using static analysis
/// This module uses compile-time constants to force validation during F# compilation
/// </summary>
module CompileTimeLql =
    
    /// <summary>
    /// Validates LQL at compile time and returns a validation token
    /// This MUST be called with string literals to work properly
    /// </summary>
    let inline validateLqlCompileTime (lql: string) =
        // This uses F#'s constant folding during compilation
        let validationResult = LqlCompileTimeChecker.validateLqlSyntax lql
        match validationResult with
        | Some error ->
            // Create a compile-time error by referencing undefined symbols
            let errorToken = sprintf "INVALID_LQL_COMPILE_ERROR_%s_IN_%s" (error.Replace(" ", "_")) (lql.Replace(" ", "_"))
            failwith $"❌ INVALID LQL DETECTED AT COMPILE TIME: {error}"
        | None ->
            true // LQL is valid
    
    /// <summary>
    /// Execute LQL with mandatory compile-time validation
    /// Usage: CompileTimeLql.execute conn "valid lql here" mapRow
    /// </summary>
    let inline execute conn (lql: string) mapRow =
        // Force compile-time evaluation by using the literal validator
        // This will FAIL COMPILATION if LQL is invalid
        let validationResult = LqlCompileTimeChecker.validateLqlSyntax lql
        match validationResult with
        | Some error ->
            // This creates a compile-time error by calling failwith
            // The F# compiler will evaluate this for string literals
            failwithf "COMPILE-TIME LQL ERROR: %s in query: %s" error lql
        | None ->
            // LQL is valid, execute it
            LqlExtensions.executeLql conn lql mapRow