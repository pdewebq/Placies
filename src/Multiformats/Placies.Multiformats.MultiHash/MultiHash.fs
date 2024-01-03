namespace Placies.Multiformats

open System
open System.IO
open System.Security.Cryptography
open System.Text
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

    /// <example>
    /// <c>sha2-256 : 256 : 2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824</c>
    /// </example>
    let toHumanReadableString (multiHashProvider: IMultiHashProvider) (multiHash: MultiHash) : string =
        let sb = StringBuilder()
        let multiHashInfo = multiHashProvider.TryGetByCode(multiHash.HashFunctionCode) |> Option.get // TODO: Error handling
        sb.Append(multiHashInfo.Name).Append(" : ") |> ignore
        sb.Append(multiHash.DigestSize * 8).Append(" : ") |> ignore
        sb.Append(multiHash.Digest.ToHexString()) |> ignore
        sb.ToString()

    // ----

    let computeFromBytes (bytes: byte array) (info: MultiHashInfo) : MultiHash =
        let digest = info.HashAlgorithm().ComputeHash(bytes)
        create info.Code digest

    let computeFromStream (stream: Stream) (info: MultiHashInfo) : MultiHash =
        let digest = info.HashAlgorithm().ComputeHash(stream)
        create info.Code digest
