namespace DataProvider.SQLite.FSharp

open System.Data
open DataProvider

/// <summary>
/// F# bindings for the existing C# DataProvider functionality
/// </summary>
module SimpleSqlite =
    
    /// <summary>
    /// Execute query using existing C# DbConnectionExtensions
    /// </summary>
    let executeQuery (connection: IDbConnection) (sql: string) mapper =
        DbConnectionExtensions.Query(connection, sql, null, mapper)
    
    /// <summary>
    /// Execute parameterized query using existing C# DbConnectionExtensions  
    /// </summary>
    let executeQueryWithParams (connection: IDbConnection) (sql: string) (parameters: IDataParameter seq) mapper =
        DbConnectionExtensions.Query(connection, sql, parameters, mapper)