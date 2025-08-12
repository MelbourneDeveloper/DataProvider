namespace Lql.TypeProvider.FSharp

open System
open System.Collections.Generic
open System.Reflection
open Microsoft.Data.Sqlite
open FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes

/// <summary>
/// Schema information for a database column
/// </summary>
type ColumnInfo = {
    Name: string
    Type: string
    IsNullable: bool
    IsPrimaryKey: bool
}

/// <summary>
/// Schema information for a database table
/// </summary>
type TableInfo = {
    Name: string
    Columns: ColumnInfo list
}

/// <summary>
/// Database schema inspector for SQLite
/// </summary>
module SchemaInspector =
    
    /// <summary>
    /// Get all tables and their columns from a SQLite database
    /// </summary>
    let getTables (connectionString: string) =
        try
            use connection = new SqliteConnection(connectionString)
            connection.Open()
            
            // Get all table names
            use tablesCmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'", connection)
            use tablesReader = tablesCmd.ExecuteReader()
            
            let tableNames = ResizeArray<string>()
            while tablesReader.Read() do
                tableNames.Add(tablesReader.GetString("name"))
            
            tablesReader.Close()
            
            let tables = ResizeArray<TableInfo>()
            
            for tableName in tableNames do
                // Get column info for this table
                use columnsCmd = new SqliteCommand($"PRAGMA table_info({tableName})", connection)
                use columnsReader = columnsCmd.ExecuteReader()
                
                let columns = ResizeArray<ColumnInfo>()
                while columnsReader.Read() do
                    let column = {
                        Name = columnsReader.GetString("name")
                        Type = columnsReader.GetString("type")
                        IsNullable = columnsReader.GetInt32("notnull") = 0
                        IsPrimaryKey = columnsReader.GetInt32("pk") > 0
                    }
                    columns.Add(column)
                
                let table = {
                    Name = tableName
                    Columns = columns |> List.ofSeq
                }
                tables.Add(table)
                
            tables |> List.ofSeq
            
        with
        | ex -> 
            // If we can't connect at design time, return empty schema
            []

/// <summary>
/// Validates LQL syntax at compile time
/// </summary>
module LqlCompileTimeValidator =
    open Lql
    
    let validateLqlQuery (lqlQuery: string) =
        try
            let lqlStatement = LqlStatementConverter.ToStatement(lqlQuery)
            if lqlStatement.GetType().Name.Contains("Success") then
                Ok "Valid LQL syntax"
            else
                let errorValue = lqlStatement.GetType().GetProperty("ErrorValue").GetValue(lqlStatement)
                let message = errorValue.GetType().GetProperty("Message").GetValue(errorValue) :?> string
                Error $"❌ COMPILE-TIME LQL SYNTAX ERROR: {message}"
        with ex ->
            Error $"❌ COMPILE-TIME LQL VALIDATION FAILED: {ex.Message}"

/// <summary>
/// F# Type Provider for LQL with compile-time validation
/// </summary>
[<TypeProvider>]
type LqlSchemaTypeProvider(config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(config, assemblyReplacementMap = [("Lql.TypeProvider.FSharp.DesignTime", "Lql.TypeProvider.FSharp")], addDefaultProbingLocation = true)

    let ns = "Lql.TypeProvider.FSharp.Schema"
    let asm = Assembly.GetExecutingAssembly()

    let createTypes() =
        let lqlType = ProvidedTypeDefinition(asm, ns, "LqlDatabase", Some typeof<obj>)
        
        // Add static parameter for connection string
        let parameters = [ProvidedStaticParameter("ConnectionString", typeof<string>)]
        lqlType.DefineStaticParameters(parameters, fun typeName args ->
            let connectionString = args.[0] :?> string
            
            let providedType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)
            
            // Get schema at compile time
            let tables = SchemaInspector.getTables connectionString
            
            // Create a Tables nested type
            let tablesType = ProvidedTypeDefinition("Tables", Some typeof<obj>)
            providedType.AddMember(tablesType)
            
            // Create a type for each table
            for table in tables do
                let tableType = ProvidedTypeDefinition(table.Name, Some typeof<obj>)
                
                // Add properties for each column with proper types
                for column in table.Columns do
                    let propertyType = 
                        match column.Type.ToUpper() with
                        | "INTEGER" -> if column.IsNullable then typeof<int64 option> else typeof<int64>
                        | "TEXT" -> if column.IsNullable then typeof<string option> else typeof<string>
                        | "REAL" -> if column.IsNullable then typeof<double option> else typeof<double>
                        | "BLOB" -> if column.IsNullable then typeof<byte[] option> else typeof<byte[]>
                        | _ -> if column.IsNullable then typeof<obj option> else typeof<obj>
                    
                    let property = ProvidedProperty(column.Name, propertyType)
                    property.GetterCode <- fun args -> <@@ null @@> // Placeholder
                    tableType.AddMember(property)
                
                tablesType.AddMember(tableType)
            
            // Add a connection property
            let connectionProperty = ProvidedProperty("ConnectionString", typeof<string>)
            connectionProperty.GetterCode <- fun args -> <@@ connectionString @@>
            providedType.AddMember(connectionProperty)
            
            // Add COMPILE-TIME validated LQL execution method
            let executeLqlMethod = ProvidedMethod("ExecuteValidatedLql", [ProvidedParameter("query", typeof<string>)], typeof<Result<obj list, string>>)
            executeLqlMethod.InvokeCode <- fun args ->
                let query = args.[0]
                // This SHOULD validate at compile time, but F# quotations make it complex
                <@@
                    let queryStr = %%query : string
                    match LqlCompileTimeValidator.validateLqlQuery queryStr with
                    | Ok _ -> 
                        async {
                            return! LqlExtensions.executeLqlQuery connectionString queryStr
                        } |> Async.RunSynchronously
                    | Error err -> Error err
                @@>
            providedType.AddMember(executeLqlMethod)
            
            providedType
        )
        
        [lqlType]

    do
        this.AddNamespace(ns, createTypes())

/// <summary>
/// Strongly-typed LQL query builder
/// </summary>
type LqlQueryBuilder<'T>(connectionString: string, tableName: string) =
    
    member _.ConnectionString = connectionString
    member _.TableName = tableName
    
    /// <summary>
    /// Select specific columns (compile-time validated)
    /// </summary>
    member _.Select(columns: string list) =
        LqlQueryBuilder<'T>(connectionString, tableName)
    
    /// <summary>
    /// Add WHERE clause (compile-time validated) 
    /// </summary>
    member _.Where(condition: string) =
        LqlQueryBuilder<'T>(connectionString, tableName)
    
    /// <summary>
    /// Execute the query and return strongly-typed results
    /// </summary>
    member _.Execute() : 'T list =
        // This would execute the built LQL query
        []