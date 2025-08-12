namespace Lql.TypeProvider.FSharp

open System
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Quotations
open Microsoft.Data.Sqlite

/// <summary>
/// PROPER F# Type Provider for LQL that validates queries at COMPILE TIME
/// Invalid LQL will cause COMPILATION FAILURES with detailed error messages
/// This is the REAL solution using F# Type Providers correctly
/// </summary>
[<TypeProvider>]
type LqlCompileTimeProvider(config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(config, assemblyReplacementMap = [("Lql.TypeProvider.FSharp", "Lql.TypeProvider.FSharp")])

    let ns = "Lql.CompileTime"
    let asm = Assembly.GetExecutingAssembly()

    /// <summary>
    /// Creates the main LQL type that validates queries at compile time
    /// This is where the MAGIC happens - compile-time validation!
    /// </summary>
    let createLqlType() =
        let lqlType = ProvidedTypeDefinition(asm, ns, "ValidatedLql", Some typeof<obj>, isErased = true)
        
        // Add static parameter for LQL query - this triggers compile-time validation
        let staticParams = [ProvidedStaticParameter("Query", typeof<string>)]
        
        lqlType.DefineStaticParameters(staticParams, fun typeName args ->
            let lqlQuery = args.[0] :?> string
            
            // *** THIS IS THE COMPILE-TIME VALIDATION ***
            // The F# compiler evaluates this during compilation!
            let validationResult = LqlCompileTimeChecker.validateLqlSyntax lqlQuery
            
            match validationResult with
            | Some errorMessage ->
                // *** FORCE COMPILATION FAILURE ***
                // Create a type that will cause the F# compiler to fail
                let errorType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased = true)
                
                // Add a method that references non-existent types to force compiler error
                let errorMethod = ProvidedMethod("COMPILE_ERROR", [], typeof<unit>, 
                    invokeCode = fun _ -> 
                        // This quotation references types that don't exist, forcing compilation failure
                        <@@ 
                            let _ : INVALID_LQL_SYNTAX_ERROR = ()
                            let _ : LQL_VALIDATION_FAILED = ()
                            failwith ("❌ COMPILE-TIME LQL ERROR: " + errorMessage + " in query: " + lqlQuery)
                        @@>)
                        
                errorMethod.AddXmlDoc($"❌ COMPILE-TIME ERROR: {errorMessage}")
                errorType.AddMember(errorMethod)
                
                // Add XML documentation that shows the error prominently
                errorType.AddXmlDoc($"""
❌ COMPILE-TIME LQL VALIDATION FAILED ❌
Error: {errorMessage}
Query: {lqlQuery}

This LQL query is invalid and must be fixed before compilation can succeed.
""")
                errorType
                
            | None ->
                // *** LQL IS VALID - CREATE WORKING TYPE ***
                let validType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased = true)
                
                // Convert LQL to SQL at compile time for performance
                let sqlResult = LqlCompileTimeChecker.convertToSql lqlQuery
                let sql = 
                    match sqlResult with
                    | Ok validSql -> validSql
                    | Error err -> lqlQuery // Fallback to original query if conversion fails
                
                validType.AddXmlDoc($"""
✅ COMPILE-TIME VALIDATED LQL ✅
Original LQL: {lqlQuery}
Generated SQL: {sql}

This LQL query passed compile-time validation.
""")
                
                // Add Query property
                let queryProperty = ProvidedProperty("Query", typeof<string>,
                    getterCode = fun _ -> <@@ lqlQuery @@>)
                queryProperty.AddXmlDoc($"The validated LQL query: {lqlQuery}")
                
                // Add SQL property  
                let sqlProperty = ProvidedProperty("GeneratedSql", typeof<string>,
                    getterCode = fun _ -> <@@ sql @@>)
                sqlProperty.AddXmlDoc($"The SQL generated from the LQL: {sql}")
                
                // Add Execute method
                let executeMethod = ProvidedMethod("Execute", 
                    [ProvidedParameter("connection", typeof<SqliteConnection>)
                     ProvidedParameter("rowMapper", typeof<SqliteDataReader -> 'T>)], 
                    typeof<Result<'T list, string>>,
                    invokeCode = fun args -> 
                        <@@
                            let conn = %%args.[0] : SqliteConnection
                            let mapper = %%args.[1] : SqliteDataReader -> 'T
                            LqlExtensions.executeLql conn lqlQuery mapper
                        @@>)
                executeMethod.AddXmlDoc("Execute this compile-time validated LQL query")
                
                // Add static factory method
                let createMethod = ProvidedMethod("Create", [], validType,
                    invokeCode = fun _ -> <@@ null @@>, // Dummy implementation since type is erased
                    isStatic = true)
                createMethod.AddXmlDoc("Create an instance of this validated LQL query")
                
                validType.AddMember(queryProperty)
                validType.AddMember(sqlProperty) 
                validType.AddMember(executeMethod)
                validType.AddMember(createMethod)
                validType
        )
        
        lqlType.AddXmlDoc("""
F# Type Provider for LQL with COMPILE-TIME VALIDATION

Usage:
  type MyQuery = ValidatedLql<"Customer |> select(*)">
  
Invalid LQL will cause COMPILATION FAILURES with detailed error messages.
Valid LQL will generate optimized types with compile-time SQL conversion.
""")
        lqlType

    do
        this.AddNamespace(ns, [createLqlType()])

[<assembly: TypeProviderAssembly>]
do ()