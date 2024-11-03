namespace Placies

#nowarn "0060" // FS0060 : Override implementations in augmentations are now deprecated. Override implementations should be given as part of the initial declaration of a type.

open System
open System.IO
open System.Text
open FsToolkit.ErrorHandling
open Placies.Utils
open Placies.Multiformats

[<StructuredFormatDisplay("{DisplayText}")>]
type Cid = {
    Version: int
    ContentTypeCode: int
    MultiHash: MultiHash
} with
    member this.DisplayText = this.ToString()

[<RequireQualifiedAccess>]
module Cid =

    let create (version: int) (contentTypeCode: int) (multiHash: MultiHash) : Cid =
        { Version = version
          ContentTypeCode = contentTypeCode
          MultiHash = multiHash }

    let encode (cid: Cid) : string =
        cid.ToString()

    let tryParseV0 (input: string) : Result<Cid, string> = result {
        let! multiHash = MultiHash.parseBase58String input |> Result.mapError string
        return {
            Version = 0
            ContentTypeCode = MultiCodecInfos.DagPb.Code
            MultiHash = multiHash
        }
    }

    let tryParseV1 (multiBaseProvider: IMultiBaseProvider) (input: string) : Result<Cid, string> = result {
        let! bytes = MultiBase.tryDecode multiBaseProvider input
        use stream = new MemoryStream(bytes)
        do! stream.ReadVarIntAsInt32() |> Result.requireEqualTo 1 "Unknown CID version"
        return {
            Version = 1
            ContentTypeCode = stream.ReadVarIntAsInt32()
            MultiHash = MultiHash.ofStream stream
        }
    }

    let tryParse (multiBaseProvider: IMultiBaseProvider) (input: string) : Result<Cid, string> = result {
        if input.Length = 46 && input.StartsWith("Qm") then
            return! tryParseV0 input
        else
            return! tryParseV1 multiBaseProvider input
    }

    let parse (multibaseProvider: IMultiBaseProvider) (input: string) : Cid =
        tryParse multibaseProvider input |> Result.getOk

    let getSize (cid: Cid) : int =
        if cid.Version = 0 then
            MultiHash.getSize cid.MultiHash
        else
            VarInt.getSizeOfInt32 cid.Version
            + VarInt.getSizeOfInt32 cid.ContentTypeCode
            + MultiHash.getSize cid.MultiHash

    let writeToSpan (cid: Cid) (buffer: Span<byte>) : int =
        if cid.Version = 0 then
            MultiHash.writeToSpan cid.MultiHash buffer
        else
            let written1 = VarInt.writeToSpanOfInt32 cid.Version buffer
            let buffer = buffer.Slice(written1)
            let written2 = VarInt.writeToSpanOfInt32 cid.ContentTypeCode buffer
            let buffer = buffer.Slice(written2)
            let written3 = MultiHash.writeToSpan cid.MultiHash buffer
            written1 + written2 + written3

    let writeToStream (stream: Stream) (cid: Cid) : unit =
        if cid.Version = 0 then
            cid.MultiHash |> MultiHash.writeToStream stream
        else
            stream.WriteVarInt(cid.Version)
            stream.WriteVarInt(cid.ContentTypeCode)
            cid.MultiHash |> MultiHash.writeToStream stream

    let ofByteArray (bytes: byte array) : Cid =
        if bytes.Length = 34 then
            create 0 MultiCodecInfos.DagPb.Code (MultiHash.ofBytes bytes)
        else
            use stream = new MemoryStream(bytes)
            let version = stream.ReadVarIntAsInt32()
            let contentType = stream.ReadVarIntAsInt32()
            let multiHash = MultiHash.ofStream stream
            create version contentType multiHash

    let toByteArray (cid: Cid) : byte array =
        let buffer = Array.zeroCreate<byte> (getSize cid)
        writeToSpan cid (buffer.AsSpan()) |> ignore
        buffer

    /// <example>
    /// <c>base32 - cidv1 - raw - (sha2-256 : 256 : 2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824)</c>
    /// </example>
    let toHumanReadableString (multiBaseInfo: MultiBaseInfo) (multiCodecProvider: IMultiCodecProvider) (multiHashProvider: IMultiHashProvider) (cid: Cid) : string =
        let sb = StringBuilder()
        sb.Append(multiBaseInfo.Name).Append(" - ") |> ignore
        sb.Append("cidv").Append(cid.Version).Append(" - ") |> ignore
        let multiCodecInfo = multiCodecProvider.TryGetByCode(cid.ContentTypeCode) |> Option.get // TODO: Error handling
        sb.Append(multiCodecInfo.Name).Append(" - ") |> ignore
        let hashHumanReadableStr = cid.MultiHash |> MultiHash.toHumanReadableString multiHashProvider
        sb.Append("(").Append(hashHumanReadableStr).Append(")") |> ignore
        sb.ToString()

type Cid with
    override this.ToString() =
        if this.Version = 0 then
            this.MultiHash |> MultiHash.toBase58String
        else
            let bytes = this |> Cid.toByteArray
            bytes |> MultiBase.encode MultiBaseInfos.Base32

type CidParser =

    static member TryParseV1FromSpan(buffer: ReadOnlySpan<byte>, bytesConsumed: int outref): Result<Cid, exn> =
        bytesConsumed <- 0
        let mutable buffer = buffer
        match VarIntParser.TryParseFromSpanAsUInt64(buffer) with
        | Ok version, consumed ->
            let version = int32<uint64> version
            bytesConsumed <- bytesConsumed + consumed
            buffer <- buffer.Slice(consumed)
            match VarIntParser.TryParseFromSpanAsUInt64(buffer) with
            | Ok contentTypeCode, consumed ->
                let contentTypeCode = int32<uint64> contentTypeCode
                bytesConsumed <- bytesConsumed + consumed
                buffer <- buffer.Slice(consumed)
                match MultiHashParser.TryParseFromSpan(buffer) with
                | Ok multiHash, consumed ->
                    buffer <- buffer.Slice(consumed)
                    bytesConsumed <- bytesConsumed + consumed
                    Ok {
                        Version = version
                        ContentTypeCode = contentTypeCode
                        MultiHash = multiHash
                    }
                | Error err, _ ->
                    Error (AggregateException("Failed parse multihash", err))
            | Error err, _ ->
                Error (AggregateException("Failed parse content type code", err))
        | Error err, _ ->
            Error (AggregateException("Failed parse version", err))

    static member TryParseV0FromSpan(buffer: ReadOnlySpan<byte>, bytesConsumed: int outref): Result<Cid, exn> =
        let mutable buffer = buffer
        bytesConsumed <- 0
        match MultiHashParser.TryParseFromSpan(buffer) with
        | Ok multiHash, consumed ->
            buffer <- buffer.Slice(consumed)
            bytesConsumed <- bytesConsumed + consumed
            Ok {
                Version = 0
                ContentTypeCode = MultiCodecInfos.DagPb.Code
                MultiHash = multiHash
            }
        | Error err, _ ->
            Error (AggregateException("Failed parse multihash", err))

    static member TryParseFromSpan(buffer: ReadOnlySpan<byte>, bytesConsumed: int outref): Result<Cid, exn> =
        if buffer.Length < 2 then
            Error (exn "Cannot identify CID version: too few bytes")
        else
            if buffer.[0] = 0x12uy && buffer.[1] = 0x20uy then
                CidParser.TryParseV0FromSpan(buffer, &bytesConsumed)
            else
                CidParser.TryParseV1FromSpan(buffer, &bytesConsumed)
