module LqlValidator

open System
open Microsoft.Data.Sqlite
open Lql
open Lql.SQLite
open Results

//TODO: this does not belong here. Move to core code

/// Validates LQL at compile time and provides execution methods
type LqlQuery private() =
    
    /// Validates and executes an LQL query
    static member inline Execute(connectionString: string, [<ReflectedDefinition>] lqlQuery: string) =
        // Validate at compile time
        let statementResult = LqlStatementConverter.ToStatement(lqlQuery)
        match statementResult with
        | :? Result<LqlStatement, SqlError>.Success as success ->
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
                
                let results = ResizeArray<Map<string, obj>>()
                while reader.Read() do
                    let row = 
                        [| for i in 0 .. reader.FieldCount - 1 ->
                            let name = reader.GetName(i)
                            let value = if reader.IsDBNull(i) then box DBNull.Value else reader.GetValue(i)
                            (name, value) |]
                        |> Map.ofArray
                    results.Add(row)
                
                Ok(results |> List.ofSeq)
            | _ -> 
                Error "Invalid LQL statement type"
        | :? Result<LqlStatement, SqlError>.Failure as failure ->
            Error(sprintf "Invalid LQL syntax: %s" failure.ErrorValue.Message)
        | _ ->
            Error "Unknown result type from LQL parser"
    
    /// Gets the SQL for an LQL query (for debugging)
    static member inline ToSql([<ReflectedDefinition>] lqlQuery: string) =
        let statementResult = LqlStatementConverter.ToStatement(lqlQuery)
        match statementResult with
        | :? Result<LqlStatement, SqlError>.Success as success ->
            let lqlStatement = success.Value
            match lqlStatement.AstNode with
            | :? Pipeline as pipeline ->
                let sqliteContext = SQLiteContext()
                Ok(PipelineProcessor.ConvertPipelineToSql(pipeline, sqliteContext))
            | _ -> 
                Error "Invalid LQL statement type"
        | :? Result<LqlStatement, SqlError>.Failure as failure ->
            Error(sprintf "Invalid LQL syntax: %s" failure.ErrorValue.Message)
        | _ ->
            Error "Unknown result type from LQL parser"