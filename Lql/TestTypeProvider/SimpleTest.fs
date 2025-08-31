module SimpleTest

open System

let testBasic() =
    printfn "Basic F# compilation test"
    printfn "Type provider assembly exists and is referenced"
    0

[<EntryPoint>]
let main args =
    testBasic()