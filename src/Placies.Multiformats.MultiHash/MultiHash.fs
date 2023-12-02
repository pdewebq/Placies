namespace Placies.Multiformats

open System
open System.Collections.Generic
open System.IO
open System.Security.Cryptography
open Placies
open Placies.Utils

type MultiHashInfo = {
    CodecInfo: MultiCodecInfo
    HashAlgorithm: unit -> HashAlgorithm
} with
    member this.Code = this.CodecInfo.Code
    member this.Name = this.CodecInfo.Name

type IMultiHashProvider =
    abstract TryGetByCode: code: int -> MultiHashInfo option
    abstract TryGetByName: name: string -> MultiHashInfo option

type MultiHash = {
    HashFunctionCode: int
    Digest: byte array
} with
    member this.DigestSize: int = this.Digest.Length

[<RequireQualifiedAccess>]
module MultiHash =

    let create (hashFunctionCode: int) (digest: byte array) : MultiHash =
        { HashFunctionCode = hashFunctionCode; Digest = digest }

    let ofStream (stream: Stream) : MultiHash =
        let hashFuncCode = stream.ReadVarint32()
        let digestSize = stream.ReadVarint32()
        let digest = Array.zeroCreate digestSize
        stream.Read(digest.AsSpan()) |> ignore
        create hashFuncCode digest

    let ofBytes (bytes: byte array) : MultiHash =
        use stream = new MemoryStream(bytes)
        ofStream stream

    let parseBase58String (input: string) : Result<MultiHash, exn> =
        Result.tryWith ^fun () ->
            MultiBaseInfos.Base58Btc.BaseEncoder.Decode(input) |> ofBytes

    let writeToStream (stream: Stream) (multiHash: MultiHash) : unit =
        stream.WriteVarint(multiHash.HashFunctionCode)
        stream.WriteVarint(multiHash.DigestSize)
        stream.Write(multiHash.Digest)

    let toBase58String (multiHash: MultiHash) : string =
        use stream = new MemoryStream()
        multiHash |> writeToStream stream
        let bytes = stream.ToArray()
        MultiBaseInfos.Base58Btc.BaseEncoder.Encode(bytes)

    // ----

    let computeFromBytes (bytes: byte array) (info: MultiHashInfo) : MultiHash =
        let digest = info.HashAlgorithm().ComputeHash(bytes)
        create info.Code digest

    let computeFromStream (stream: Stream) (info: MultiHashInfo) : MultiHash =
        let digest = info.HashAlgorithm().ComputeHash(stream)
        create info.Code digest


[<RequireQualifiedAccess>]
module MultiHashInfos =

    let Sha2_256 = { CodecInfo = MultiCodecInfos.Sha2_256; HashAlgorithm = fun () -> SHA256.Create() }


type MultiHashRegistry() =
    let registryOfName = Dictionary<string, MultiHashInfo>()
    let registryOfCode = Dictionary<int, MultiHashInfo>()

    member this.Register(info: MultiHashInfo): bool =
        registryOfCode.TryAdd(info.Code, info)
        && registryOfName.TryAdd(info.Name, info)

    interface IMultiHashProvider with
        member this.TryGetByCode(code) =
            registryOfCode.TryGetValue(code) |> Option.ofTryByref
        member this.TryGetByName(name) =
            registryOfName.TryGetValue(name) |> Option.ofTryByref

    static member CreateDefault(): MultiHashRegistry =
        let registry = MultiHashRegistry()
        registry.Register(MultiHashInfos.Sha2_256) |> ignore
        registry
