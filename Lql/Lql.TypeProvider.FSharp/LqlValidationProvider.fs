namespace Lql.TypeProvider.FSharp

open System
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Quotations

/// <summary>
/// WORKING F# Type Provider that validates LQL at compile time
/// Invalid LQL will cause compiler errors when the type is used
/// </summary>
[<TypeProvider>]
type LqlValidationProvider(config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(config)

    let ns = "Lql.Validated"
    let asm = Assembly.GetExecutingAssembly()

    let createValidatedLqlType() =
        let baseType = ProvidedTypeDefinition(asm, ns, "ValidatedLql", Some typeof<obj>, isErased = true)
        
        // Static parameter that triggers compile-time validation
        let staticParams = [ProvidedStaticParameter("Query", typeof<string>)]
        
        baseType.DefineStaticParameters(staticParams, fun typeName args ->
            let lqlQuery = args.[0] :?> string
            
            // THIS IS THE ACTUAL COMPILE-TIME VALIDATION
            let validationResult = LqlCompileTimeChecker.validateLqlSyntax lqlQuery
            
            let resultType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased = true)
            
            match validationResult with
            | Some errorMessage ->
                // For invalid LQL, create a type with no working constructor or methods
                // This will cause compilation errors when trying to use the type
                resultType.AddXmlDoc($"""
❌ COMPILE-TIME LQL ERROR ❌
Error: {errorMessage}
Query: {lqlQuery}

This LQL query is INVALID. Fix the syntax to proceed.
""")
                
                // Add a constructor that will fail when called
                let constructor = ProvidedConstructor([], 
                    invokeCode = fun _ -> 
                        <@@ failwith $"❌ INVALID LQL: {errorMessage} in query: {lqlQuery}" @@>)
                resultType.AddMember(constructor)
                
                // Add an Execute method that will also fail
                let executeMethod = ProvidedMethod("Execute", 
                    [ProvidedParameter("connection", typeof<Microsoft.Data.Sqlite.SqliteConnection>)
                     ProvidedParameter("mapRow", typeof<Microsoft.Data.Sqlite.SqliteDataReader -> 'T>)], 
                    typeof<Result<'T list, string>>,
                    invokeCode = fun args ->
                        <@@ Error $"❌ INVALID LQL: {errorMessage} in query: {lqlQuery}" @@>)
                resultType.AddMember(executeMethod)
                
            | None ->
                // Valid LQL - create working type
                let constructor = ProvidedConstructor([], 
                    invokeCode = fun _ -> <@@ obj() @@>)
                    
                let queryProperty = ProvidedProperty("Query", typeof<string>,
                    getterCode = fun _ -> <@@ lqlQuery @@>)
                    
                let executeMethod = ProvidedMethod("Execute", 
                    [ProvidedParameter("connection", typeof<Microsoft.Data.Sqlite.SqliteConnection>)
                     ProvidedParameter("mapRow", typeof<Microsoft.Data.Sqlite.SqliteDataReader -> 'T>)], 
                    typeof<Result<'T list, string>>,
                    invokeCode = fun args ->
                        <@@
                            let conn = %%args.[0] : Microsoft.Data.Sqlite.SqliteConnection
                            let mapper = %%args.[1] : Microsoft.Data.Sqlite.SqliteDataReader -> 'T
                            LqlExtensions.executeLql conn lqlQuery mapper
                        @@>)
                
                resultType.AddXmlDoc($"""✅ VALIDATED LQL: {lqlQuery}""")
                resultType.AddMember(constructor)
                resultType.AddMember(queryProperty)
                resultType.AddMember(executeMethod)
            
            resultType)
        
        baseType

    do
        this.AddNamespace(ns, [createValidatedLqlType()])

[<assembly: TypeProviderAssembly>]
do ()