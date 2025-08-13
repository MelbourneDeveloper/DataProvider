namespace Lql.TypeProvider.SQLite

open System
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Quotations
open Microsoft.Data.Sqlite
open System.IO
open Lql
open Lql.SQLite
open Results

[<TypeProvider>]
type LqlSqliteTypeProvider(config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(config, addDefaultProbingLocation = true)
    
    let ns = "Lql.TypeProvider.SQLite"
    let asm = Assembly.GetExecutingAssembly()
    let tempAssembly = ProvidedAssembly()
    
    let createTypes(typeName: string) =
        let myType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased = false)
        
        let parameters = [
            ProvidedStaticParameter("ConnectionString", typeof<string>)
            ProvidedStaticParameter("LqlQuery", typeof<string>, parameterDefaultValue = "")
            ProvidedStaticParameter("LqlFile", typeof<string>, parameterDefaultValue = "")
        ]
        
        myType.DefineStaticParameters(
            parameters,
            fun typeName args ->
                let connectionString = args.[0] :?> string
                let lqlQuery = args.[1] :?> string
                let lqlFile = args.[2] :?> string
                
                let resolvedLqlFile = 
                    if String.IsNullOrWhiteSpace(lqlFile) then ""
                    else Path.Combine(config.ResolutionFolder, lqlFile)
                
                let lql = 
                    if not (String.IsNullOrWhiteSpace(lqlQuery)) then
                        lqlQuery
                    elif not (String.IsNullOrWhiteSpace(resolvedLqlFile)) && File.Exists(resolvedLqlFile) then
                        File.ReadAllText(resolvedLqlFile)
                    else
                        failwith "Either LqlQuery or LqlFile must be provided"
                
                // Validate LQL at compile time and convert to SQL
                let sql = 
                    let statementResult = LqlStatementConverter.ToStatement(lql)
                    match statementResult with
                    | :? Result<LqlStatement, SqlError>.Success as success ->
                        let lqlStatement = success.Value
                        match lqlStatement.AstNode with
                        | :? Pipeline as pipeline ->
                            let sqliteContext = SQLiteContext()
                            PipelineProcessor.ConvertPipelineToSql(pipeline, sqliteContext)
                        | _ -> 
                            failwithf "Invalid LQL statement type"
                    | :? Result<LqlStatement, SqlError>.Failure as failure ->
                        failwithf "Invalid LQL syntax: %s" failure.ErrorValue.Message
                    | _ ->
                        failwith "Unknown result type from LQL parser"
                
                // Create the provided type
                let providedType = ProvidedTypeDefinition(typeName, Some typeof<obj>, isErased = false)
                
                // Add the original LQL as a property
                let lqlProp = ProvidedProperty("LqlQuery", typeof<string>, isStatic = true, getterCode = fun _ -> <@@ lql @@>)
                providedType.AddMember(lqlProp)
                
                // Add the generated SQL as a property
                let sqlProp = ProvidedProperty("GeneratedSql", typeof<string>, isStatic = true, getterCode = fun _ -> <@@ sql @@>)
                providedType.AddMember(sqlProp)
                
                // Create a Result type to represent query results
                let resultType = ProvidedTypeDefinition("QueryResult", Some typeof<obj>, isErased = false)
                providedType.AddMember(resultType)
                
                // Try to get schema information if possible
                let tryGetSchema() =
                    try
                        use conn = new SqliteConnection(connectionString)
                        conn.Open()
                        use cmd = new SqliteCommand(sql + " LIMIT 0", conn)
                        use reader = cmd.ExecuteReader()
                        
                        [| for i in 0 .. reader.FieldCount - 1 ->
                            let name = reader.GetName(i)
                            let fieldType = reader.GetFieldType(i)
                            (name, fieldType) |]
                    with _ ->
                        // If we can't connect at design time, provide generic schema
                        [||]
                
                let schema = tryGetSchema()
                
                // Add properties for each column in the result
                for (columnName, columnType) in schema do
                    let prop = ProvidedProperty(columnName, columnType, getterCode = fun args -> 
                        <@@ 
                            let row = %%args.[0] : obj
                            let dict = row :?> System.Collections.Generic.Dictionary<string, obj>
                            dict.[columnName]
                        @@>)
                    resultType.AddMember(prop)
                
                // Create Execute method that returns strongly typed results
                let executeMethod = 
                    ProvidedMethod(
                        "Execute",
                        [],
                        typeof<ResizeArray<obj>>,
                        isStatic = true,
                        invokeCode = fun _ ->
                            <@@
                                let results = ResizeArray<obj>()
                                use conn = new SqliteConnection(connectionString)
                                conn.Open()
                                use cmd = new SqliteCommand(sql, conn)
                                use reader = cmd.ExecuteReader()
                                
                                while reader.Read() do
                                    let row = System.Collections.Generic.Dictionary<string, obj>()
                                    for i in 0 .. reader.FieldCount - 1 do
                                        let name = reader.GetName(i)
                                        let value = if reader.IsDBNull(i) then null else reader.GetValue(i)
                                        row.[name] <- value
                                    results.Add(box row)
                                
                                results
                            @@>
                    )
                
                providedType.AddMember(executeMethod)
                
                // Create ExecuteAsync method
                let executeAsyncMethod = 
                    ProvidedMethod(
                        "ExecuteAsync",
                        [],
                        typeof<Async<ResizeArray<obj>>>,
                        isStatic = true,
                        invokeCode = fun _ ->
                            <@@
                                async {
                                    let results = ResizeArray<obj>()
                                    use conn = new SqliteConnection(connectionString)
                                    do! conn.OpenAsync() |> Async.AwaitTask
                                    use cmd = new SqliteCommand(sql, conn)
                                    use! reader = cmd.ExecuteReaderAsync() |> Async.AwaitTask
                                    
                                    let rec readRows() = async {
                                        let! hasRow = reader.ReadAsync() |> Async.AwaitTask
                                        if hasRow then
                                            let row = System.Collections.Generic.Dictionary<string, obj>()
                                            for i in 0 .. reader.FieldCount - 1 do
                                                let name = reader.GetName(i)
                                                let value = if reader.IsDBNull(i) then null else reader.GetValue(i)
                                                row.[name] <- value
                                            results.Add(box row)
                                            return! readRows()
                                    }
                                    
                                    do! readRows()
                                    return results
                                }
                            @@>
                    )
                
                providedType.AddMember(executeAsyncMethod)
                
                // Create a DataContext type for more advanced scenarios
                let dataContextType = ProvidedTypeDefinition("DataContext", Some typeof<obj>, isErased = false)
                providedType.AddMember(dataContextType)
                
                let createMethod = 
                    ProvidedMethod(
                        "Create",
                        [],
                        dataContextType,
                        isStatic = true,
                        invokeCode = fun _ -> <@@ obj() @@>
                    )
                providedType.AddMember(createMethod)
                
                tempAssembly.AddTypes([providedType])
                providedType
        )
        
        myType
    
    let providedType = createTypes "LqlProvider"
    
    do
        this.AddNamespace(ns, [providedType])

[<TypeProvider>]
type LqlFileTypeProvider(config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(config, addDefaultProbingLocation = true)
    
    let ns = "Lql.TypeProvider.SQLite"
    let asm = Assembly.GetExecutingAssembly()
    
    // Scan for .lql files in the project
    let lqlFiles = 
        try
            Directory.GetFiles(config.ResolutionFolder, "*.lql", SearchOption.AllDirectories)
            |> Array.map (fun path -> 
                let relativePath = Path.GetRelativePath(config.ResolutionFolder, path)
                let typeName = Path.GetFileNameWithoutExtension(path).Replace(" ", "_").Replace("-", "_")
                (typeName, path, relativePath))
        with _ -> [||]
    
    // Create a type for each .lql file found
    let types = 
        lqlFiles
        |> Array.map (fun (typeName, fullPath, relativePath) ->
            let providedType = ProvidedTypeDefinition(asm, ns, typeName + "Query", Some typeof<obj>, isErased = true)
            
            // Add static parameter for connection string
            let parameters = [ProvidedStaticParameter("ConnectionString", typeof<string>)]
            
            providedType.DefineStaticParameters(
                parameters,
                fun innerTypeName args ->
                    let connectionString = args.[0] :?> string
                    let lql = File.ReadAllText(fullPath)
                    
                    // Validate LQL at compile time and convert to SQL
                    let sql = 
                        let statementResult = LqlStatementConverter.ToStatement(lql)
                        match statementResult with
                        | :? Result<LqlStatement, SqlError>.Success as success ->
                            let lqlStatement = success.Value
                            match lqlStatement.AstNode with
                            | :? Pipeline as pipeline ->
                                let sqliteContext = SQLiteContext()
                                PipelineProcessor.ConvertPipelineToSql(pipeline, sqliteContext)
                            | _ -> 
                                failwithf "Invalid LQL statement in file %s" relativePath
                        | :? Result<LqlStatement, SqlError>.Failure as failure ->
                            failwithf "Invalid LQL syntax in file %s: %s" relativePath failure.ErrorValue.Message
                        | _ ->
                            failwithf "Unknown result type from LQL parser for file %s" relativePath
                    
                    let innerType = ProvidedTypeDefinition(innerTypeName, Some typeof<obj>, isErased = true)
                    
                    // Add properties
                    let fileProp = ProvidedProperty("FilePath", typeof<string>, isStatic = true, getterCode = fun _ -> <@@ relativePath @@>)
                    innerType.AddMember(fileProp)
                    
                    let lqlProp = ProvidedProperty("LqlQuery", typeof<string>, isStatic = true, getterCode = fun _ -> <@@ lql @@>)
                    innerType.AddMember(lqlProp)
                    
                    let sqlProp = ProvidedProperty("GeneratedSql", typeof<string>, isStatic = true, getterCode = fun _ -> <@@ sql @@>)
                    innerType.AddMember(sqlProp)
                    
                    // Add Execute method
                    let executeMethod = 
                        ProvidedMethod(
                            "Execute",
                            [],
                            typeof<ResizeArray<Map<string, obj>>>,
                            isStatic = true,
                            invokeCode = fun _ ->
                                <@@
                                    let results = ResizeArray<Map<string, obj>>()
                                    use conn = new SqliteConnection(connectionString)
                                    conn.Open()
                                    use cmd = new SqliteCommand(sql, conn)
                                    use reader = cmd.ExecuteReader()
                                    
                                    while reader.Read() do
                                        let row = 
                                            [| for i in 0 .. reader.FieldCount - 1 ->
                                                let name = reader.GetName(i)
                                                let value = if reader.IsDBNull(i) then null else reader.GetValue(i)
                                                (name, value) |]
                                            |> Map.ofArray
                                        results.Add(row)
                                    
                                    results
                                @@>
                        )
                    innerType.AddMember(executeMethod)
                    
                    innerType
            )
            
            providedType
        )
        |> Array.toList
    
    do
        this.AddNamespace(ns, types)

[<assembly: TypeProviderAssembly>]
do ()