#r "Lql.TypeProvider.FSharp/bin/Debug/net9.0/Lql.TypeProvider.FSharp.dll"
#r "Lql.TypeProvider.SQLite/bin/Debug/net9.0/Lql.TypeProvider.SQLite.dll"

//TODO: delete this or move to the correct location

open Lql
open Lql.SQLite

// Test the basic LQL type provider
type ValidQuery = LqlCommand<"Customer |> select(*)">
type InvalidQuery = LqlCommand<"Customer |> invalid_syntax">  // This should fail at compile time

// Test SQLite-specific provider
type SqliteQuery = Lql.SQLite.LqlCommand<"Customer |> select(*)", "Data Source=test.db">

printfn "LQL Type Provider Test:"
printfn "Valid Query: %s" ValidQuery.Query
printfn "Valid SQL: %s" ValidQuery.Sql

printfn "\nSQLite Query: %s" SqliteQuery.Query
printfn "SQLite SQL: %s" SqliteQuery.Sql