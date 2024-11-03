namespace Placies.Ipld.DagPb

open System
open System.Buffers
open System.Text
open FsToolkit.ErrorHandling
open Placies

// Based on https://github.com/ipld/js-dag-pb/blob/27ed1722e3d1788e40f03186a1dc9b1ba69fd1d2/src/pb-encode.js

[<RequireQualifiedAccess>]
module DagPbEncode =

    /// work out exactly how many bytes this link takes up
    let sizeLink (link: PBLink) : int =
        let mutable n = 0

        let hashSize = link.Hash |> Cid.getSize
        n <- n + 1 + hashSize + VarInt.getSizeOfInt32 hashSize

        match link.Name with
        | ValueSome name ->
            let nameSize = Encoding.UTF8.GetByteCount(name)
            n <- n + 1 + nameSize + VarInt.getSizeOfInt32 nameSize
        | ValueNone -> ()

        match link.Tsize with
        | ValueSome tsize ->
            n <- n + 1 + VarInt.getSizeOfUInt64 tsize
        | ValueNone -> ()

        n

    /// Work out exactly how many bytes this node takes up
    let sizeNode (node: PBNode) : int =
        let mutable n = 0

        match node.Data with
        | ValueSome data ->
            let dataSize = data.Length
            n <- n + 1 + dataSize + VarInt.getSizeOfInt32 dataSize
        | ValueNone -> ()

        for link in node.Links do
            let linkSize = sizeLink link
            n <- n + 1 + linkSize + VarInt.getSizeOfInt32 linkSize

        n

    let writeVarIntOfUInt64 (buffer: Span<byte>) (offset: int) (v: uint64) : int =
        let offset = offset - VarInt.getSizeOfUInt64 v
        VarInt.writeToSpanOfUInt64 v (buffer.Slice(offset)) |> ignore
        offset

    let writeVarIntOfInt32 (buffer: Span<byte>) (offset: int) (v: int) : int =
        writeVarIntOfUInt64 buffer offset (uint64<int32> v)

    let writeLink (link: PBLink) (buffer: Span<byte>) : int =
        let mutable i = buffer.Length

        match link.Tsize with
        | ValueSome tsize ->
            i <- writeVarIntOfUInt64 buffer i tsize - 1
            buffer.[i] <- 0x18uy
        | ValueNone -> ()

        match link.Name with
        | ValueSome name ->
            let nameBytes = Encoding.UTF8.GetBytes(name)
            i <- i - nameBytes.Length
            nameBytes.CopyTo(buffer.Slice(i))
            i <- writeVarIntOfInt32 buffer i nameBytes.Length - 1
            buffer.[i] <- 0x12uy
        | ValueNone -> ()

        let cid = link.Hash
        let cidSize = Cid.getSize cid
        i <- i - cidSize
        Cid.writeToSpan cid (buffer.Slice(i)) |> ignore
        i <- writeVarIntOfInt32 buffer i cidSize - 1
        buffer.[i] <- 0x0Auy

        buffer.Length - i


    let writeNode (node: PBNode) (bufferWriter: IBufferWriter<byte>) : unit =
        let nodeSize = sizeNode node
        let buffer = bufferWriter.GetSpan(nodeSize)
        let mutable i = nodeSize

        match node.Data with
        | ValueSome data ->
            i <- i - data.Length
            data.Span.CopyTo(buffer.Slice(i))
            i <- writeVarIntOfInt32 buffer i data.Length - 1
            buffer.[i] <- 0x0Auy
        | ValueNone -> ()

        for index in node.Links.Count-1 .. -1 .. 0 do
            let linkSize = writeLink node.Links.[index] (buffer.Slice(0, i))
            i <- i - linkSize
            i <- writeVarIntOfInt32 buffer i linkSize - 1
            buffer.[i] <- 0x12uy

        bufferWriter.Advance(nodeSize)
