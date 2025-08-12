namespace Lql.TypeProvider.FSharp

open System
open Results
open Lql
open Lql.SQLite

/// <summary>
/// Provides compile-time validation for LQL queries using the C# Lql library
/// This module handles Result types properly and provides detailed error messages
/// </summary>
module LqlCompileTimeChecker =
    
    /// <summary>
    /// Validates LQL syntax at compile time using the C# LqlStatementConverter
    /// </summary>
    /// <param name="lqlQuery">The LQL query string to validate</param>
    /// <returns>None if valid, Some(errorMessage) if invalid</returns>
    let validateLqlSyntax (lqlQuery: string) : string option =
        if String.IsNullOrWhiteSpace lqlQuery then
            Some "LQL query cannot be null or empty"
        else
            let result = LqlStatementConverter.ToStatement lqlQuery
            match result with
            | :? Results.Result<LqlStatement, SqlError>.Success -> None // Valid LQL
            | :? Results.Result<LqlStatement, SqlError>.Failure as failure ->
                let error = failure.ErrorValue
                let position = 
                    match error.Position with
                    | null -> ""
                    | pos -> $" at line {pos.Line}, column {pos.Column}"
                Some $"LQL syntax error: {error.Message}{position}"
            | _ -> Some "Unknown error occurred during LQL parsing"
    
    /// <summary>
    /// Gets a comprehensive validation result for the LQL query
    /// </summary>
    /// <param name="lqlQuery">The LQL query string to validate</param>
    let getValidationResult (lqlQuery: string) =
        match String.IsNullOrWhiteSpace lqlQuery with
        | true -> Error "LQL query cannot be null or empty"
        | false ->
            match LqlStatementConverter.ToStatement lqlQuery with
            | :? Results.Result<LqlStatement, SqlError>.Success as success -> 
                Ok success.Value
            | :? Results.Result<LqlStatement, SqlError>.Failure as failure ->
                let error = failure.ErrorValue
                let position = 
                    match error.Position with
                    | null -> ""
                    | pos -> $" at line {pos.Line}, column {pos.Column}"
                Error $"LQL syntax error: {error.Message}{position}"
            | _ -> 
                Error "Unknown error occurred during LQL parsing"
    
    /// <summary>
    /// Converts LQL to SQL without executing, with proper error handling
    /// </summary>
    /// <param name="lqlQuery">The LQL query string</param>
    let convertToSql (lqlQuery: string) =
        let lqlResult = LqlStatementConverter.ToStatement lqlQuery
        match lqlResult with
        | :? Results.Result<LqlStatement, SqlError>.Success as success ->
            // For now, convert to SQLite SQL - could be parameterized later
            let sqlResult = success.Value.ToSQLite()
            match sqlResult with
            | :? Results.Result<string, SqlError>.Success as sqlSuccess ->
                Ok sqlSuccess.Value
            | :? Results.Result<string, SqlError>.Failure as sqlFailure ->
                Error $"SQL generation error: {sqlFailure.ErrorValue.Message}"
            | _ -> Error "Unknown error during SQL generation"
        | :? Results.Result<LqlStatement, SqlError>.Failure as failure ->
            Error $"LQL parse error: {failure.ErrorValue.Message}"
        | _ -> Error "Unknown error during LQL parsing"
    
    /// <summary>
    /// Gets detailed validation information for tooling/debugging
    /// </summary>
    /// <param name="lqlQuery">The LQL query string</param>
    let getValidationInfo (lqlQuery: string) : {| IsValid: bool; ErrorMessage: string option; Query: string |} =
        let errorMessage = validateLqlSyntax lqlQuery
        {| 
            IsValid = Option.isNone errorMessage
            ErrorMessage = errorMessage
            Query = lqlQuery 
        |}