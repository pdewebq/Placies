[<AutoOpen>]
module IpfsSigningGatewayProxy.Utils

open System
open System.Diagnostics.CodeAnalysis


[<return: Struct>]
let (|Equals|_|) x y = if x = y then ValueSome () else ValueNone

let (|Regex|_|) ([<StringSyntax(StringSyntaxAttribute.Regex)>] pattern: string) (input: string) : string list option =
    let m = System.Text.RegularExpressions.Regex.Match(input, pattern)
    if not m.Success then
        None
    else
        m.Groups |> Seq.skip 1 |> Seq.map (fun g -> g.Value) |> Seq.toList
        |> Some


[<Obsolete("TODO")>]
let todo<'a> : 'a = failwith "TODO"


[<RequireQualifiedAccess>]
module Result =

    let getOk (res: Result<'a, 'e>) : 'a =
        match res with
        | Ok a -> a
        | Error e -> failwith $"%A{e}"

    let tryWith (func: unit -> 'a) : Result<'a, exn> =
        try
            Ok (func ())
        with ex ->
            Error ex


[<RequireQualifiedAccess>]
module Option =

    let ofTryByref (isSuccess: bool, value: 'a) : 'a option =
        match isSuccess, value with
        | true, value -> Some value
        | false, _ -> None
