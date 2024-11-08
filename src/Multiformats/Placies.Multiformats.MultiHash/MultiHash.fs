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
    Digest: byte array // TODO: Change to ReadOnlyMemory
} with
    member this.DigestSize: int = this.Digest.Length

type MultiHashParser =
    static member TryParseFromSpan(buffer: ReadOnlySpan<byte>, bytesConsumed: int outref): Result<MultiHash, exn> =
        bytesConsumed <- 0
        let mutable buffer = buffer
        match VarIntParser.TryParseFromSpanAsUInt64(buffer) with
        | Ok hashFuncCode, consumed ->
            let hashFuncCode = int32<uint64> hashFuncCode
            bytesConsumed <- bytesConsumed + consumed
            buffer <- buffer.Slice(consumed)
            match VarIntParser.TryParseFromSpanAsUInt64(buffer) with
            | Ok digestSize, consumed ->
                let digestSize = int32<uint64> digestSize
                bytesConsumed <- bytesConsumed + consumed
                buffer <- buffer.Slice(consumed)
                if digestSize > buffer.Length then
                    Error (exn "Unexpected end of data")
                else
                    let digest = buffer.Slice(0, digestSize).ToArray()
                    buffer <- buffer.Slice(digestSize)
                    bytesConsumed <- bytesConsumed + digestSize
                    Ok {
                        HashFunctionCode = hashFuncCode
                        Digest = digest
                    }
            | Error err, _ ->
                Error (AggregateException("Failed parse digest size", err))
        | Error err, _ ->
            Error (AggregateException("Failed parse hash function code", err))

[<RequireQualifiedAccess>]
module MultiHash =

    let create (hashFunctionCode: int) (digest: byte array) : MultiHash =
        { HashFunctionCode = hashFunctionCode; Digest = digest }

    let ofStream (stream: Stream) : MultiHash =
        let hashFuncCode = stream.ReadVarIntAsInt32()
        let digestSize = stream.ReadVarIntAsInt32()
        let digest = Array.zeroCreate digestSize
        stream.Read(digest.AsSpan()) |> ignore
        create hashFuncCode digest

    let ofBytes (bytes: byte array) : MultiHash =
        use stream = new MemoryStream(bytes)
        ofStream stream

    let parseBase58String (input: string) : Result<MultiHash, exn> =
        Result.tryWith ^fun () ->
            MultiBaseInfos.Base58Btc.BaseCoder.Decode(input.AsMemory()) |> ofBytes

    let getSize (multiHash: MultiHash) : int =
        VarInt.getSizeOfInt32 multiHash.HashFunctionCode
        + VarInt.getSizeOfInt32 multiHash.DigestSize
        + multiHash.DigestSize

    let writeToSpan (multiHash: MultiHash) (buffer: Span<byte>) : int =
        let written1 = VarInt.writeToSpanOfInt32 multiHash.HashFunctionCode buffer
        let buffer = buffer.Slice(written1)
        let written2 = VarInt.writeToSpanOfInt32 multiHash.DigestSize buffer
        let buffer = buffer.Slice(written2)
        multiHash.Digest.CopyTo(buffer)
        let written3 = multiHash.Digest.Length
        written1 + written2 + written3

    let writeToStream (stream: Stream) (multiHash: MultiHash) : unit =
        stream.WriteVarInt(multiHash.HashFunctionCode)
        stream.WriteVarInt(multiHash.DigestSize)
        stream.Write(multiHash.Digest)

    let toBase58String (multiHash: MultiHash) : string =
        use stream = new MemoryStream()
        multiHash |> writeToStream stream
        let bytes = stream.ToArray()
        MultiBaseInfos.Base58Btc.BaseCoder.Encode(bytes)

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
