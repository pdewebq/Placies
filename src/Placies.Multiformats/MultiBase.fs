namespace Placies.Multiformats

open System
open System.Collections.Generic
open FsToolkit.ErrorHandling
open Placies

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


[<RequireQualifiedAccess>]
module MultiBaseInfos =

    open System.Text
    open System.Numerics

    let encodeAnyBase (baseAlphabet: string) (input: byte array) : string =
        let mutable number = BigInteger(input)
        let l = baseAlphabet.Length
        let result = StringBuilder()
        while number > BigInteger.Zero do
            let idx = number % BigInteger(l) |> int
            if idx >= l then failwith ""
            result.Append(baseAlphabet.[idx]) |> ignore
            number <- number / BigInteger(l)
        result.ToString()

    let decodeAnyBase (baseAlphabet: string) (input: string) : byte array =
        let mutable result = BigInteger(0)
        let b = baseAlphabet.Length
        let mutable pow = 0
        for c in input.ToCharArray() |> Array.rev |> String do
            let idx = baseAlphabet.IndexOf(c)
            if idx = -1 then failwith ""
            result <- result + BigInteger.Pow(b, pow) * BigInteger(idx)
            pow <- pow + 1
        result.ToByteArray(isUnsigned=true, isBigEndian=true)

    let base36Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz"


    let Base16 = {
        Name = "base16"; PrefixCharacter = 'f'
        BaseEncoder = BaseEncoder.create (fun bytes -> SimpleBase.Base16.LowerCase.Encode(bytes)) (fun text -> SimpleBase.Base16.LowerCase.Decode(text))
    }
    let Base32 = {
        Name = "base32"; PrefixCharacter = 'b'
        BaseEncoder = BaseEncoder.create (fun bytes -> SimpleBase.Base32.Rfc4648.Encode(bytes, false).ToLowerInvariant()) (fun text -> SimpleBase.Base32.Rfc4648.Decode(text))
    }
    let Base36 = {
        Name = "base36"; PrefixCharacter = 'k'
        BaseEncoder = BaseEncoder.create (fun bytes -> encodeAnyBase base36Alphabet bytes) (fun text -> decodeAnyBase base36Alphabet text)
    }
    let Base58Btc = {
        Name = "base58btc"; PrefixCharacter = 'z'
        BaseEncoder = BaseEncoder.create (fun bytes -> SimpleBase.Base58.Bitcoin.Encode(bytes)) (fun text -> SimpleBase.Base58.Bitcoin.Decode(text))
    }
    let Base64 = {
        Name = "base64"; PrefixCharacter = 'm'
        BaseEncoder = BaseEncoder.create (fun bytes -> Convert.ToBase64StringNoPad(bytes)) (fun text -> Convert.FromBase64StringNoPad(text))
    }
    let Base64Pad = {
        Name = "base64pad"; PrefixCharacter = 'M'
        BaseEncoder = BaseEncoder.create (fun bytes -> Convert.ToBase64String(bytes)) (fun text -> Convert.FromBase64String(text))
    }
    let Base64Url = {
        Name = "base64url"; PrefixCharacter = 'u'
        BaseEncoder = BaseEncoder.create (fun bytes -> Convert.ToBase64StringUrl(bytes)) (fun text -> Convert.FromBase64StringUrl(text))
    }


type MultiBaseRegistry() =
    let registryByPrefix = Dictionary<char, MultiBaseInfo>()
    let registryByName = Dictionary<string, MultiBaseInfo>()

    member _.Register(info: MultiBaseInfo): bool =
        registryByName.TryAdd(info.Name, info)
        && registryByPrefix.TryAdd(info.PrefixCharacter, info)

    interface IMultiBaseProvider with
        member this.TryGetByName(name) =
            registryByName.TryGetValue(name) |> Option.ofTryByref
        member this.TryGetByPrefix(prefix) =
            registryByPrefix.TryGetValue(prefix) |> Option.ofTryByref

    static member CreateDefault(): MultiBaseRegistry =
        let registry = MultiBaseRegistry()
        registry.Register(MultiBaseInfos.Base16) |> ignore
        registry.Register(MultiBaseInfos.Base32) |> ignore
        registry.Register(MultiBaseInfos.Base36) |> ignore
        registry.Register(MultiBaseInfos.Base58Btc) |> ignore
        registry.Register(MultiBaseInfos.Base64) |> ignore
        registry.Register(MultiBaseInfos.Base64Url) |> ignore
        registry