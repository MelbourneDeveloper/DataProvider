namespace Lql.TypeProvider

open System
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes
open Lql
open Lql.SQLite
open Results

[<TypeProvider>]
type public LqlTypeProvider(config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(config)

    let namespaceName = "Lql"
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

    let rootType = ProvidedTypeDefinition(thisAssembly, namespaceName, "LqlCommand", Some typeof<obj>, isErased = false)
    
    do
        rootType.DefineStaticParameters(
            [ProvidedStaticParameter("Query", typeof<string>)],
            fun typeName args ->
                let lqlQuery = args.[0] :?> string
                
                // *** COMPILE-TIME VALIDATION ***
                if String.IsNullOrWhiteSpace lqlQuery then 
                    invalidArg "Query" "LQL query cannot be null or empty!"
                
                try
                    let result = LqlStatementConverter.ToStatement lqlQuery
                    match result with
                    | :? Results.Result<LqlStatement, SqlError>.Success as success ->
                        // Valid LQL - convert to SQL
                        let sqlResult = success.Value.ToSQLite()
                        match sqlResult with
                        | :? Results.Result<string, SqlError>.Success as sqlSuccess ->
                            let sql = sqlSuccess.Value
                            createValidatedType(typeName, lqlQuery, sql)
                        | :? Results.Result<string, SqlError>.Failure as sqlFailure ->
                            failwith (sprintf "❌ COMPILATION FAILED: SQL generation error - %s for LQL: '%s'" sqlFailure.ErrorValue.Message lqlQuery)
                        | _ -> 
                            failwith (sprintf "❌ COMPILATION FAILED: Unknown SQL generation error for LQL: '%s'" lqlQuery)
                    | :? Results.Result<LqlStatement, SqlError>.Failure as failure ->
                        let error = failure.ErrorValue
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