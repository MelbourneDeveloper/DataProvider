namespace Nimblesite.Lql.Core.TypeProvider

open System
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes
open Nimblesite.Lql.Core
open Nimblesite.Lql.SQLite
open Outcome
open Selecta

[<TypeProvider>]
type public Nimblesite.Lql.CoreTypeProvider(config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(config)

    let namespaceName = "Nimblesite.Lql.Core"
    let thisAssembly = Assembly.GetExecutingAssembly()

    let createValidatedType(typeName: string, lqlQuery: string, sql: string) =
        let t = ProvidedTypeDefinition(thisAssembly, namespaceName, typeName, Some typeof<obj>, isErased = true)
        
        // Add static Query property
        let queryProp = ProvidedProperty("Query", typeof<string>, isStatic = true, getterCode = fun _ -> <@@ lqlQuery @@>)
        queryProp.AddXmlDoc(sprintf "The validated LQL query: %s" lqlQuery)
        t.AddMember(queryProp)
        
        // Add static Sql property  
        let sqlProp = ProvidedProperty("Sql", typeof<string>, isStatic = true, getterCode = fun _ -> <@@ sql @@>)
        sqlProp.AddXmlDoc(sprintf "The generated SQL: %s" sql)
        t.AddMember(sqlProp)
        
        t.AddXmlDoc(sprintf "✅ Compile-time validated LQL: '%s' → SQL: '%s'" lqlQuery sql)
        t

    let rootType = ProvidedTypeDefinition(thisAssembly, namespaceName, "Nimblesite.Lql.CoreCommand", Some typeof<obj>, isErased = true)
    
    do
        rootType.DefineStaticParameters(
            [ProvidedStaticParameter("Query", typeof<string>)],
            fun typeName args ->
                let lqlQuery = args.[0] :?> string
                
                // *** COMPILE-TIME VALIDATION ***
                if String.IsNullOrWhiteSpace lqlQuery then 
                    invalidArg "Query" "LQL query cannot be null or empty!"
                
                try
                    let result = Nimblesite.Lql.CoreStatementConverter.ToStatement lqlQuery
                    match result with
                    | :? Outcome.Result<Nimblesite.Lql.CoreStatement, SqlError>.Ok<Nimblesite.Lql.CoreStatement, SqlError> as success ->
                        // Valid LQL - convert to SQL
                        let sqlResult = success.Value.ToSQLite()
                        match sqlResult with
                        | :? Outcome.Result<string, SqlError>.Ok<string, SqlError> as sqlSuccess ->
                            let sql = sqlSuccess.Value
                            createValidatedType(typeName, lqlQuery, sql)
                        | :? Outcome.Result<string, SqlError>.Error<string, SqlError> as sqlFailure ->
                            failwith (sprintf "❌ COMPILATION FAILED: SQL generation error - %s for LQL: '%s'" sqlFailure.Value.Message lqlQuery)
                        | _ ->
                            failwith (sprintf "❌ COMPILATION FAILED: Unknown SQL generation error for LQL: '%s'" lqlQuery)
                    | :? Outcome.Result<Nimblesite.Lql.CoreStatement, SqlError>.Error<Nimblesite.Lql.CoreStatement, SqlError> as failure ->
                        let error = failure.Value
                        let position = 
                            match error.Position with
                            | null -> ""
                            | pos -> sprintf " at line %d, column %d" pos.Line pos.Column
                        failwith (sprintf "❌ COMPILATION FAILED: Invalid LQL syntax - %s%s in query: '%s'" error.Message position lqlQuery)
                    | _ -> 
                        failwith (sprintf "❌ COMPILATION FAILED: Unknown LQL parsing error in query: '%s'" lqlQuery)
                with ex ->
                    failwith (sprintf "❌ COMPILATION FAILED: Exception during validation: %s for LQL: '%s'" ex.Message lqlQuery)
        )
        
        this.AddNamespace(namespaceName, [rootType])

[<assembly: TypeProviderAssembly>]
do ()