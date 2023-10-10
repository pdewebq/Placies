[<AutoOpen>]
module Placies.Utils

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

let inline ( ^ ) f x = f x


[<RequireQualifiedAccess>]
module Result =

    let getOkOrRaise (onError: 'e -> exn) (res: Result<'a, 'e>) : 'a =
        match res with
        | Ok a -> a
        | Error e -> raise (onError e)


    let getOk (res: Result<'a, 'e>) : 'a =
        match res with
        | Ok a -> a
        | Error e -> failwith $"%A{e}"

    let tryWith (func: unit -> 'a) : Result<'a, exn> =
        try
            Ok (func ())
        with ex ->
            Error ex


type ResultExn<'a> = Result<'a, exn>

[<RequireQualifiedAccess>]
module ResultExn =

    let getOk (resExn: ResultExn<'a>) : 'a =
        match resExn with
        | Ok a -> a
        | Error ex -> raise ex


[<RequireQualifiedAccess>]
module Option =

    let ofTryByref (isSuccess: bool, value: 'a) : 'a option =
        match isSuccess, value with
        | true, value -> Some value
        | false, _ -> None


[<RequireQualifiedAccess>]
module ArraySegment =

    // [<return: Struct>]
    let (|Nil|Cons|) (source: ArraySegment<'a>) =
        if source.Count = 0 then
            Nil
        else
            Cons (source.[0], source.Slice(1))


[<RequireQualifiedAccess>]
module Array =

    let tryExactlyTwo (source: 'a array) : ('a * 'a) option =
        if source.Length = 2 then
            Some (source.[0], source.[1])
        else
            None

    let exactlyTwo (source: 'a array) : 'a * 'a =
        tryExactlyTwo source |> Option.get
