namespace DataProvider.SQLite.FSharp

open System

/// <summary>
/// Pure functional types for SQLite operations
/// </summary>
module SqliteTypes =

    /// <summary>
    /// Represents a database connection configuration
    /// </summary>
    type ConnectionConfig = {
        ConnectionString: string
        Timeout: TimeSpan option
    }
    
    /// <summary>
    /// Represents a SQL parameter
    /// </summary>
    type SqlParameter = {
        Name: string
        Value: obj
        DbType: System.Data.DbType option
    }
    
    /// <summary>
    /// Represents a database column metadata
    /// </summary>
    type ColumnInfo = {
        Name: string
        Type: string
        IsNullable: bool
        IsPrimaryKey: bool
        DefaultValue: string option
    }
    
    /// <summary>
    /// Represents a database table metadata  
    /// </summary>
    type TableInfo = {
        Name: string
        Columns: ColumnInfo list
        Schema: string option
    }
    
    /// <summary>
    /// Represents a query result row
    /// </summary>
    type ResultRow = Map<string, obj>
    
    /// <summary>
    /// Represents a SQL query with parameters
    /// </summary>
    type SqlQuery = {
        Statement: string
        Parameters: SqlParameter list
    }
    
    /// <summary>
    /// Represents transaction isolation levels
    /// </summary>
    type TransactionLevel =
        | ReadUncommitted
        | ReadCommitted  
        | RepeatableRead
        | Serializable
    
    /// <summary>
    /// Creates a connection configuration with default timeout
    /// </summary>
    let createConnectionConfig connectionString =
        { ConnectionString = connectionString; Timeout = Some (TimeSpan.FromSeconds(30.0)) }
    
    /// <summary>
    /// Creates a SQL parameter
    /// </summary>
    let createParameter name value = 
        { Name = name; Value = value; DbType = None }
    
    /// <summary>
    /// Creates a SQL parameter with explicit type
    /// </summary>
    let createTypedParameter name value dbType =
        { Name = name; Value = value; DbType = Some dbType }
    
    /// <summary>
    /// Creates a SQL query without parameters
    /// </summary>
    let createQuery statement = 
        { Statement = statement; Parameters = [] }
    
    /// <summary>
    /// Creates a SQL query with parameters
    /// </summary>
    let createQueryWithParams statement parameters =
        { Statement = statement; Parameters = parameters }