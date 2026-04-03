module Nimblesite.Lql.CoreValidator

open System
open Microsoft.Data.Sqlite
open Nimblesite.Lql.Core
open Nimblesite.Lql.SQLite
open Outcome
open Selecta

//TODO: this does not belong here. Move to core code

/// Validates LQL at compile time and provides execution methods
type Nimblesite.Lql.CoreQuery private() =
    
    /// Validates and executes an LQL query
    static member inline Execute(connectionString: string, [<ReflectedDefinition>] lqlQuery: string) =
        // Validate at compile time
        let statementResult = Nimblesite.Lql.CoreStatementConverter.ToStatement(lqlQuery)
        match statementResult with
        | :? Outcome.Result<Nimblesite.Lql.CoreStatement, SqlError>.Ok<Nimblesite.Lql.CoreStatement, SqlError> as success ->
            let lqlStatement = success.Value
            match lqlStatement.AstNode with
            | :? Pipeline as pipeline ->
                let sqliteContext = SQLiteContext()
                let sql = PipelineProcessor.ConvertPipelineToSql(pipeline, sqliteContext)
                
                // Execute the query
                use conn = new SqliteConnection(connectionString)
                conn.Open()
                use cmd = new SqliteCommand(sql, conn)
                use reader = cmd.ExecuteReader()
                
                let readColumnValue (reader: SqliteDataReader) (i: int) : obj =
                    if reader.IsDBNull(i) then
                        DBNull.Value :> obj
                    else
                        let raw: obj | null = reader.GetValue(i)
                        match raw with
                        | NonNull v -> v
                        | _ -> DBNull.Value :> obj

                let results = ResizeArray<Map<string, obj>>()
                while reader.Read() do
                    let row =
                        [| for i in 0 .. reader.FieldCount - 1 ->
                            let name = reader.GetName(i)
                            let value = readColumnValue reader i
                            (name, value) |]
                        |> Map.ofArray
                    results.Add(row)
                
                Ok(results |> List.ofSeq)
            | _ -> 
                Error "Invalid LQL statement type"
        | :? Outcome.Result<Nimblesite.Lql.CoreStatement, SqlError>.Error<Nimblesite.Lql.CoreStatement, SqlError> as failure ->
            Error(sprintf "Invalid LQL syntax: %s" failure.Value.Message)
        | _ ->
            Error "Unknown result type from LQL parser"
    
    /// Gets the SQL for an LQL query (for debugging)
    static member inline ToSql([<ReflectedDefinition>] lqlQuery: string) =
        let statementResult = Nimblesite.Lql.CoreStatementConverter.ToStatement(lqlQuery)
        match statementResult with
        | :? Outcome.Result<Nimblesite.Lql.CoreStatement, SqlError>.Ok<Nimblesite.Lql.CoreStatement, SqlError> as success ->
            let lqlStatement = success.Value
            match lqlStatement.AstNode with
            | :? Pipeline as pipeline ->
                let sqliteContext = SQLiteContext()
                Ok(PipelineProcessor.ConvertPipelineToSql(pipeline, sqliteContext))
            | _ -> 
                Error "Invalid LQL statement type"
        | :? Outcome.Result<Nimblesite.Lql.CoreStatement, SqlError>.Error<Nimblesite.Lql.CoreStatement, SqlError> as failure ->
            Error(sprintf "Invalid LQL syntax: %s" failure.Value.Message)
        | _ ->
            Error "Unknown result type from LQL parser"