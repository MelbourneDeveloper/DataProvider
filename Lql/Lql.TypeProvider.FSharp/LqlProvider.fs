namespace Lql.TypeProvider.FSharp

open System
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Quotations
open Microsoft.Data.Sqlite
open Lql

/// <summary>
/// TRUE F# Type Provider for LQL that validates ALL queries at compile time
/// Invalid LQL will cause COMPILATION FAILURES
/// </summary>
[<TypeProvider>]
type LqlProvider(config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(config, assemblyReplacementMap = [("Lql.TypeProvider.FSharp", "Lql.TypeProvider.FSharp")])

    let ns = "Lql"
    let asm = Assembly.GetExecutingAssembly()

    /// <summary>
    /// Creates the main LQL type that validates queries at compile time
    /// </summary>
    let createLqlType() =
        let lqlType = ProvidedTypeDefinition(asm, ns, "Lql", Some typeof<obj>, isErased = true)
        
        // Add static parameter for LQL query - this is where compile-time validation happens
        let staticParams = [ProvidedStaticParameter("Query", typeof<string>)]
        
        lqlType.DefineStaticParameters(staticParams, fun typeName args ->
            let lqlQuery = args.[0] :?> string
            
            // COMPILE-TIME VALIDATION - This happens during F# compilation!
            let validationResult = LqlCompileTimeChecker.validateLqlSyntax lqlQuery
            
            match validationResult with
            | Some errorMessage ->
                // Create a type that will cause a COMPILE-TIME ERROR
                let errorType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased = true)
                
                // Add XML documentation that shows the error
                errorType.AddXmlDoc($"❌ COMPILE-TIME LQL ERROR: {errorMessage}")
                
                // Create a property that will fail at compile time
                let errorProp = ProvidedProperty("COMPILE_TIME_LQL_ERROR", typeof<string>, 
                    getterCode = fun _ -> 
                        // This will cause a compile-time error with the validation message
                        failwith $"❌ INVALID LQL DETECTED AT COMPILE TIME: {errorMessage} in query: '{lqlQuery}'")
                        
                errorProp.AddXmlDoc($"COMPILE ERROR: {errorMessage}")
                errorType.AddMember(errorProp)
                errorType
                
            | None ->
                // LQL is valid - create the execution type
                let validType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased = true)
                
                // Convert LQL to SQL at compile time
                let sqlResult = LqlCompileTimeChecker.convertToSql lqlQuery
                let sql = 
                    match sqlResult with
                    | Ok validSql -> validSql
                    | Error err -> failwith $"❌ LQL to SQL conversion failed: {err}"
                
                validType.AddXmlDoc($"✅ Compile-time validated LQL: {lqlQuery} → SQL: {sql}")
                
                // Add Query property
                let queryProp = ProvidedProperty("Query", typeof<string>,
                    getterCode = fun _ -> <@@ lqlQuery @@>)
                queryProp.AddXmlDoc($"The validated LQL query: {lqlQuery}")
                
                // Add SQL property  
                let sqlProp = ProvidedProperty("Sql", typeof<string>,
                    getterCode = fun _ -> <@@ sql @@>)
                sqlProp.AddXmlDoc($"The generated SQL: {sql}")
                
                // Add Execute method that takes connection and row mapper
                let executeMethod = ProvidedMethod("Execute", 
                    [ProvidedParameter("conn", typeof<SqliteConnection>)
                     ProvidedParameter("mapRow", typeof<SqliteDataReader -> 'T>)], 
                    typeof<Result<'T list, string>>,
                    invokeCode = fun args -> 
                        <@@
                            let conn = %%args.[0] : SqliteConnection
                            let mapRow = %%args.[1] : SqliteDataReader -> 'T
                            LqlExtensions.executeLql conn lqlQuery mapRow
                        @@>)
                executeMethod.AddXmlDoc("Execute this compile-time validated LQL query")
                
                validType.AddMember(queryProp)
                validType.AddMember(sqlProp)
                validType.AddMember(executeMethod)
                validType
        )
        
        lqlType.AddXmlDoc("LQL Type Provider - validates ALL queries at compile time")
        lqlType

    do
        this.AddNamespace(ns, [createLqlType()])

[<assembly: TypeProviderAssembly>]
do ()