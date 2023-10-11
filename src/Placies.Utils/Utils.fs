namespace Placies.Utils

open System
open System.Diagnostics.CodeAnalysis

[<AutoOpen>]
module GeneralUtils =

    [<Obsolete("TODO")>]
    let todo<'a> : 'a = failwith "TODO"

    let inline ( ^ ) f x = f x

[<AutoOpen>]
module Patterns =

    [<return: Struct>]
    let (|Equals|_|) x y = if x = y then ValueSome () else ValueNone

    let (|Regex|_|) ([<StringSyntax(StringSyntaxAttribute.Regex)>] pattern: string) (input: string) : string list option =
        let m = System.Text.RegularExpressions.Regex.Match(input, pattern)
        if not m.Success then
            None
        else
            m.Groups |> Seq.skip 1 |> Seq.map (fun g -> g.Value) |> Seq.toList
            |> Some
