module Lql.Analyzer.FSharp

open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTrivia
open System.Collections.Immutable
open Lql

/// <summary>
/// F# Analyzer that validates LQL queries at compile time
/// This will generate F# compiler errors for invalid LQL
/// </summary>
[<Analyzer "LqlCompileTimeValidator">]
let lqlAnalyzer : Analyzer =
    fun (context: Context) ->
        let checkLqlString (range: FSharp.Compiler.Text.Range) (lqlQuery: string) =
            // Validate the LQL using the existing validation logic
            let converter = LqlStatementConverter()
            let result = converter.ConvertLqlToSql(lqlQuery)
            
            if not result.Success then
                // Create a compiler error message
                let message = sprintf "Invalid LQL syntax: %s in query: '%s'" result.ErrorMessage lqlQuery
                
                // Return a diagnostic that will show as a compiler error
                {
                    Type = "LQL001"
                    Message = message
                    Code = "LQL001" 
                    Severity = Error
                    Range = range
                    Fixes = []
                }
                |> Some
            else
                None
        
        let rec visitSynExpr (expr: SynExpr) =
            match expr with
            | SynExpr.App (_, _, funcExpr, argExpr, _) ->
                // Check if this is a call to LQL execution functions
                match funcExpr with
                | SynExpr.LongIdent (_, SynLongIdent([ident1; ident2], _, _), _, _) when 
                    ident1.idText = "LqlApi" && ident2.idText = "executeLql" ->
                    
                    // Look for string literal arguments
                    match argExpr with
                    | SynExpr.Const (SynConst.String (lqlQuery, _, _), range) ->
                        checkLqlString range lqlQuery
                    | _ -> None
                        
                | SynExpr.LongIdent (_, SynLongIdent([ident1; ident2], _, _), _, _) when 
                    ident1.idText = "CompileTimeErrors" && ident2.idText = "executeLql" ->
                    
                    // Look for string literal arguments  
                    match argExpr with
                    | SynExpr.Const (SynConst.String (lqlQuery, _, _), range) ->
                        checkLqlString range lqlQuery
                    | _ -> None
                        
                | _ -> None
                
            | _ -> None
        
        let rec visitSynModuleDecl (decl: SynModuleDecl) =
            match decl with
            | SynModuleDecl.Let (_, bindings, _) ->
                bindings
                |> List.choose (fun binding ->
                    match binding with
                    | SynBinding (_, _, _, _, _, _, _, _, _, expr, _, _, _) ->
                        visitSynExpr expr
                )
            | _ -> []
        
        // Visit all module declarations in the file
        let diagnostics =
            match context.ParseTree with
            | ParsedInput.ImplFile (ParsedImplFileInput (_, _, _, _, _, modules, _, _, _)) ->
                modules
                |> List.collect (fun (SynModuleOrNamespace (_, _, _, decls, _, _, _, _, _)) ->
                    decls |> List.collect visitSynModuleDecl)
            | _ -> []
        
        // Return the diagnostics as an immutable array
        diagnostics |> List.choose id |> List.toArray |> ImmutableArray.CreateRange