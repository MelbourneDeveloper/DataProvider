open System
open Microsoft.Data.Sqlite

// Reference the type provider
open Lql

printfn "Testing LQL Type Provider (FSharp.Data.SqlClient pattern)"
printfn "============================================================"

// This should work - valid LQL
type ValidQuery = LqlCommand<"Customer |> select(*)">

printfn "✅ Valid LQL Query:"
printfn "   LQL: %s" ValidQuery.Query  
printfn "   SQL: %s" ValidQuery.Sql

// Another valid query
type FilterQuery = LqlCommand<"Customer |> filter(age > 25) |> select(name, age)">

printfn "\n✅ Valid Filter Query:"
printfn "   LQL: %s" FilterQuery.Query
printfn "   SQL: %s" FilterQuery.Sql

// This should cause a COMPILE-TIME ERROR when uncommented:
// Uncomment the line below to see the compilation fail:
// type InvalidQuery = LqlCommand<"Customer |> seflect(*)">  // misspelled "select" as "seflect"

printfn "\n🎉 Type provider validation working!"
printfn "   - Valid queries compile successfully"
printfn "   - SQL generation works at compile time"
printfn "   - Invalid queries would cause compilation to fail"
printfn "\nTo test compilation failure, uncomment the InvalidQuery line in Program.fs"

[<EntryPoint>]
let main args =
    0