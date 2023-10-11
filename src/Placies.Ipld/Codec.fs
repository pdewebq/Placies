namespace Placies.Ipld

open System.IO
open FsToolkit.ErrorHandling
open Placies
open Placies.Multiformats

type ICodec =
    abstract CodecInfo: MultiCodecInfo
    abstract TryEncodeAsync: writeToStream: Stream * dataModelNode: DataModelNode -> TaskResult<unit, exn>
    abstract TryDecodeAsync: stream: Stream -> TaskResult<DataModelNode, exn>

[<AutoOpen>]
module CodecExtensions =
    type ICodec with
        member this.TryEncodeWithCidAsync(writeToStream: Stream, dataModelNode: DataModelNode, cidVersion: int, cidMultihashInfo: MultiHashInfo): TaskResult<Cid, exn> = taskResult {
            use stream = new MemoryStream()
            do! this.TryEncodeAsync(stream, dataModelNode)
            stream.Seek(0, SeekOrigin.Begin) |> ignore
            let multihash = MultiHash.computeFromStream stream cidMultihashInfo
            let cid = Cid.create cidVersion this.CodecInfo.Code multihash
            stream.Seek(0, SeekOrigin.Begin) |> ignore
            stream.CopyTo(writeToStream)
            return cid
        }
