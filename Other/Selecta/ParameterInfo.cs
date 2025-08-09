namespace Selecta;

/// <summary>
/// Represents a parameter in the SQL query
/// </summary>
public sealed record ParameterInfo(string Name, string SqlType = "NVARCHAR");
