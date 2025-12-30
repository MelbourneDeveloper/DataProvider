using System.Diagnostics.CodeAnalysis;

namespace DataProvider.SQLite.Parsing;

/// <summary>
/// Listener to determine SQLite query type from parse tree
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SqliteQueryTypeListener : SQLiteParserBaseListener
{
    /// <summary>
    /// Gets the detected query type (e.g. SELECT, INSERT, UPDATE, DELETE).
    /// </summary>
    public string QueryType { get; private set; } = "UNKNOWN";

    /// <summary>
    /// Called when entering a SELECT statement node.
    /// </summary>
    /// <param name="context">The parser context.</param>
    public override void EnterSelect_stmt(SQLiteParser.Select_stmtContext context) =>
        QueryType = "SELECT";

    /// <summary>
    /// Called when entering an INSERT statement node.
    /// </summary>
    /// <param name="context">The parser context.</param>
    public override void EnterInsert_stmt(SQLiteParser.Insert_stmtContext context) =>
        QueryType = "INSERT";

    /// <summary>
    /// Called when entering an UPDATE statement node.
    /// </summary>
    /// <param name="context">The parser context.</param>
    public override void EnterUpdate_stmt(SQLiteParser.Update_stmtContext context) =>
        QueryType = "UPDATE";

    /// <summary>
    /// Called when entering a DELETE statement node.
    /// </summary>
    /// <param name="context">The parser context.</param>
    public override void EnterDelete_stmt(SQLiteParser.Delete_stmtContext context) =>
        QueryType = "DELETE";

    /// <summary>
    /// Called when entering a CREATE TABLE statement node.
    /// </summary>
    /// <param name="context">The parser context.</param>
    public override void EnterCreate_table_stmt(SQLiteParser.Create_table_stmtContext context) =>
        QueryType = "CREATE_TABLE";

    /// <summary>
    /// Called when entering a DROP statement node.
    /// </summary>
    /// <param name="context">The parser context.</param>
    public override void EnterDrop_stmt(SQLiteParser.Drop_stmtContext context) =>
        QueryType = "DROP";
}
