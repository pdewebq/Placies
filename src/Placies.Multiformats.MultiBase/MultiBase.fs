namespace Placies.Multiformats

open FsToolkit.ErrorHandling
open Placies.Utils


type IBaseEncoder =
    abstract Encode: bytes: byte array -> string
    abstract Decode: str: string -> byte array

[<RequireQualifiedAccess>]
module BaseEncoder =
    let create (encode: byte array -> string) (decode: string -> byte array) : IBaseEncoder =
        { new IBaseEncoder with
            member _.Encode(bytes) = encode bytes
            member _.Decode(str) = decode str
        }

type MultiBaseInfo = {
    Name: string
    PrefixCharacter: char
    BaseEncoder: IBaseEncoder
}

type IMultiBaseProvider =
    abstract TryGetByName: name: string -> MultiBaseInfo option
    abstract TryGetByPrefix: prefix: char -> MultiBaseInfo option

[<RequireQualifiedAccess>]
module MultiBase =

    let encode (multibaseInfo: MultiBaseInfo) (bytes: byte array) : string =
        string multibaseInfo.PrefixCharacter + multibaseInfo.BaseEncoder.Encode(bytes)

    let tryDecode (provider: IMultiBaseProvider) (multibaseText: string) : Result<byte array, string> = result {
        let! prefix = multibaseText |> Seq.tryHead |> Result.requireSome "No multibase prefix"
        let text = multibaseText.Substring(1)
        let! multibaseInfo = provider.TryGetByPrefix(prefix) |> Result.requireSome $"Not found multibase for prefix '%c{prefix}'"
        return multibaseInfo.BaseEncoder.Decode(text)
    }

    let decide provider multibaseText =
        tryDecode provider multibaseText |> Result.getOk
