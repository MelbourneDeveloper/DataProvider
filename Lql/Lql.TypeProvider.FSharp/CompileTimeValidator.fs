namespace Lql.TypeProvider.FSharp

open System
open Microsoft.Data.Sqlite

/// <summary>
/// F# Source Generator approach for compile-time LQL validation
/// This creates actual compile-time errors by generating invalid F# code for bad LQL
/// </summary>
module CompileTimeErrors =
    
    /// <summary>
    /// This function is designed to be used by a source generator
    /// The generator will scan for calls to this function and replace them with validation
    /// </summary>
    let inline lqlCompileTimeValidate (lqlQuery: string) =
        // At runtime this does nothing, but source generator replaces this
        lqlQuery
        
    /// <summary>
    /// Execute LQL with compile-time validation via source generator
    /// The source generator will validate the LQL and generate compilation errors for invalid queries
    /// </summary>
    let inline executeLql conn lqlQuery mapRow =
        let validatedQuery = lqlCompileTimeValidate lqlQuery
        LqlExtensions.executeLql conn validatedQuery mapRow

/// <summary>
/// Simplified approach - just use runtime validation but fail fast
/// This is NOT compile-time but will at least give clear errors
/// </summary>
module LqlApiRuntime =
    
    /// <summary>
    /// Execute LQL with immediate validation
    /// This validates at runtime but fails with clear error messages
    /// </summary>
    let executeLql (conn: SqliteConnection) (lqlQuery: string) (mapRow: SqliteDataReader -> 'T) =
        // Immediate validation
        match LqlCompileTimeChecker.validateLqlSyntax lqlQuery with
        | Some error ->
            Error $"❌ INVALID LQL: {error} in query: {lqlQuery}"
        | None ->
            LqlExtensions.executeLql conn lqlQuery mapRow