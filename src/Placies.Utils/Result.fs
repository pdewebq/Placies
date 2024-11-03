namespace Placies.Utils

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

    let inline requireTrueWith (errorThunk: unit -> 'error) (value: bool) : Result<unit, 'error> =
        if value then Ok () else Error (errorThunk ())

// ----

type ResultExn<'a> = Result<'a, exn>

[<RequireQualifiedAccess>]
module ResultExn =

    let getOk (resExn: ResultExn<'a>) : 'a =
        match resExn with
        | Ok a -> a
        | Error ex -> raise ex
