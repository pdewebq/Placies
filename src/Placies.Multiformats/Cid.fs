namespace Placies

#nowarn "0060" // FS0060 : Override implementations in augmentations are now deprecated. Override implementations should be given as part of the initial declaration of a type.

open System.IO
open FsToolkit.ErrorHandling
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

    let tryParse (multibaseProvider: IMultiBaseProvider) (input: string) : Result<Cid, string> = result {
        if input.Length = 46 && input.StartsWith("Qm") then
            let! multiHash = MultiHash.parseBase58String input |> Result.mapError string
            return {
                Version = 0
                ContentTypeCode = MultiCodecInfos.DagPb.Code
                MultiHash = multiHash
            }
        else
            let! bytes = MultiBase.tryDecode multibaseProvider input
            use stream = new MemoryStream(bytes)
            do! stream.ReadVarint32() |> Result.requireEqualTo 1 "Unknown CID version"
            return {
                Version = 1
                ContentTypeCode = stream.ReadVarint32()
                MultiHash = MultiHash.ofStream stream
            }
    }

    let parse (multibaseProvider: IMultiBaseProvider) (input: string) : Cid =
        tryParse multibaseProvider input |> Result.getOk

    let writeToStream (stream: Stream) (cid: Cid) : unit =
        if cid.Version = 0 then
            cid.MultiHash |> MultiHash.writeToStream stream
        else
            stream.WriteVarint(cid.Version)
            stream.WriteVarint(cid.ContentTypeCode)
            cid.MultiHash |> MultiHash.writeToStream stream

    let ofByteArray (bytes: byte array) : Cid =
        if bytes.Length = 34 then
            create 0 MultiCodecInfos.DagPb.Code (MultiHash.ofBytes bytes)
        else
            use stream = new MemoryStream(bytes)
            let version = stream.ReadVarint32()
            let contentType = stream.ReadVarint32()
            let multiHash = MultiHash.ofStream stream
            create version contentType multiHash

    let toByteArray (cid: Cid) : byte array =
        use stream = new MemoryStream()
        cid |> writeToStream stream
        stream.ToArray()

type Cid with
    override this.ToString() =
        if this.Version = 0 then
            this.MultiHash |> MultiHash.toBase58String
        else
            let bytes = this |> Cid.toByteArray
            bytes |> MultiBase.encode MultiBaseInfos.Base32
