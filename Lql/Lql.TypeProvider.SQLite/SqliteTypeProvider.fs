namespace Lql.TypeProvider.SQLite

open System
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Quotations
open Microsoft.Data.Sqlite
open Lql
open Lql.SQLite

/// <summary>
/// SQLite-specific LQL Type Provider that validates queries at compile-time
/// This will catch "selecht" and other syntax errors when you build!
/// </summary>
[<TypeProvider>]
type LqlSqliteProvider(config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(config, addDefaultProbingLocation = true)


[<assembly: TypeProviderAssembly>]
do ()