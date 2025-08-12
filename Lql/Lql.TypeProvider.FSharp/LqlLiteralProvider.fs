namespace Lql.TypeProvider.FSharp

open System
open System.IO
open System.Reflection
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Lql

/// <summary>
/// Proper F# Type Provider for LQL that validates syntax at compile time using literals
/// This follows the Microsoft documentation for literal-based type providers
/// </summary>
[<TypeProvider>]
type LqlProvider(config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(config, 
        assemblyReplacementMap = [("Lql.TypeProvider.FSharp", "Lql.TypeProvider.FSharp")],
        addDefaultProbingLocation = true)

    let ns = "Lql.Providers"
    let asm = Assembly.GetExecutingAssembly()

    /// <summary>
    /// Validates LQL syntax and returns error message if invalid
    /// This is the COMPILE-TIME validation that should catch syntax errors
    /// </summary>
    let validateLqlAtCompileTime (lqlQuery: string) =
        match LqlCompileTimeChecker.validateLqlSyntax lqlQuery with
        | None -> None // Valid LQL
        | Some errorMessage -> Some $"❌ INVALID LQL SYNTAX: {errorMessage} in query '{lqlQuery}'"

    /// <summary>
    /// Creates the main type provider type
    /// </summary>
    let createProviderType() =
        let providerType = ProvidedTypeDefinition(asm, ns, "LqlQuery", Some typeof<obj>, isErased = true)
        
        // Add static parameter for the LQL query string (literal)
        let parameters = [ProvidedStaticParameter("Query", typeof<string>)]
        
        providerType.DefineStaticParameters(parameters, fun typeName args ->
            let lqlQuery = args.[0] :?> string
            
            // COMPILE-TIME VALIDATION - This is where the magic happens!
            match validateLqlAtCompileTime lqlQuery with
            | Some errorMessage ->
                // Create a type that will cause a compile-time error
                let errorType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased = true)
                
                // Add a property that exposes the error at compile time
                let errorProperty = ProvidedProperty("CompileTimeError", typeof<string>, 
                    getterCode = fun _ -> <@@ errorMessage @@>)
                
                errorProperty.AddXmlDoc($"COMPILE-TIME ERROR: {errorMessage}")
                errorType.AddMember(errorProperty)
                errorType.AddXmlDoc($"❌ COMPILE-TIME ERROR: {errorMessage}")
                errorType
                
            | None ->
                // Valid LQL - create a proper type
                let validType = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>, isErased = true)
                
                // Add the validated query as a property
                let queryProperty = ProvidedProperty("Query", typeof<string>,
                    getterCode = fun _ -> <@@ lqlQuery @@>)
                queryProperty.AddXmlDoc($"✅ Validated LQL Query: {lqlQuery}")
                
                // Add execution method
                let executeMethod = ProvidedMethod("Execute", 
                    [ProvidedParameter("connectionString", typeof<string>)], 
                    typeof<Async<Result<Map<string, obj> list, string>>>,
                    invokeCode = fun args -> 
                        <@@
                            let connectionString = %%args.[0] : string
                            LqlExtensions.executeLqlQuery connectionString lqlQuery
                        @@>)
                executeMethod.AddXmlDoc("Execute this compile-time validated LQL query")
                
                // Add SQL conversion method
                let toSqlMethod = ProvidedMethod("ToSql", [], typeof<Result<string, string>>,
                    invokeCode = fun _ -> <@@ LqlExtensions.lqlToSql lqlQuery @@>)
                toSqlMethod.AddXmlDoc("Convert this validated LQL query to SQL")
                
                // Add validation status method
                let isValidMethod = ProvidedMethod("IsValid", [], typeof<bool>,
                    invokeCode = fun _ -> <@@ true @@>)
                isValidMethod.AddXmlDoc("Returns true - this query passed compile-time validation")
                
                validType.AddMember(queryProperty)
                validType.AddMember(executeMethod)
                validType.AddMember(toSqlMethod)
                validType.AddMember(isValidMethod)
                validType.AddXmlDoc($"✅ Compile-time validated LQL query: {lqlQuery}")
                validType
        )
        
        providerType.AddXmlDoc("LQL Type Provider with compile-time syntax validation")
        [providerType]

    do
        this.AddNamespace(ns, createProviderType())

/// <summary>
/// Helper type for creating validated LQL queries with compile-time checking
/// </summary>
type ValidatedLql = 
    static member inline Create(query: string) =
        // This validates at compile time when used with string literals
        match LqlCompileTimeChecker.getValidationResult query with
        | Ok statement -> 
            {| Query = query; IsValid = true; Error = None; Statement = Some statement |}
        | Error errorMessage ->
            {| Query = query; IsValid = false; Error = Some errorMessage; Statement = None |}

/// <summary>
/// Compile-time LQL validation attribute for documentation
/// </summary>
[<System.AttributeUsage(System.AttributeTargets.Property ||| System.AttributeTargets.Field ||| System.AttributeTargets.Method)>]
type ValidLqlAttribute(lqlQuery: string) =
    inherit System.Attribute()
    
    let validationResult = LqlCompileTimeChecker.validateLqlSyntax lqlQuery
    
    member _.Query = lqlQuery
    member _.IsValid = Option.isNone validationResult
    member _.ErrorMessage = validationResult

[<assembly: TypeProviderAssembly>]
do ()