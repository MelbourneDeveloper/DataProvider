See this:
https://learn.microsoft.com/en-us/dotnet/fsharp/tutorials/type-providers/

This library leverages the C# project Lql and Lql.SQLite for being able to embed SQL in F# projects as Type providers. At compile time, it parses the Lql (with the C# library), converts to platform specific SQL (with the C# library) and connects to the database where it interrogates the query metadata such as the columns (with the C# library).

It needs to return direct compiler errors when the Lql syntax is wrong, or references invalid columns or tables.

IDIOMATIC F# PLEASE!