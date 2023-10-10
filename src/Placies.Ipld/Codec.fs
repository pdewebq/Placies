namespace Placies.Ipld

open System.IO
open FsToolkit.ErrorHandling
open Placies
open Placies.Multiformats

type ICodec =
    abstract CodecInfo: MultiCodecInfo
    abstract Encode: writeToStream: Stream * dataModelNode: DataModelNode -> Result<unit, exn>
    abstract Decode: stream: Stream -> Result<DataModelNode, exn>

[<RequireQualifiedAccess>]
module Codec =

    let encodeWithCid (codec: ICodec) (version: int) (multihashInfo: MultiHashInfo) (dataModelNode: DataModelNode) (writeToStream: Stream) : Result<Cid, exn> = result {
        use stream = new MemoryStream()
        do! codec.Encode(stream, dataModelNode)
        stream.Seek(0, SeekOrigin.Begin) |> ignore
        let multihash = MultiHash.computeFromStream stream multihashInfo
        let cid = Cid.create version codec.CodecInfo.Code multihash
        stream.Seek(0, SeekOrigin.Begin) |> ignore
        stream.CopyTo(writeToStream)
        return cid
    }
