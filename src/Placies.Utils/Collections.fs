namespace Placies.Utils.Collections

open System


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
