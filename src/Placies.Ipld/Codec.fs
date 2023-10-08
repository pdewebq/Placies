namespace Placies.Ipld

open System.IO
open FsToolkit.ErrorHandling
open Ipfs
open Placies

type ICodec =
    abstract CodecName: string
    abstract Encode: writeToStream: Stream * dataModelNode: DataModelNode -> unit
    abstract Decode: stream: Stream -> Result<DataModelNode, exn>

[<AutoOpen>]
module CodecExtensions =
    type ICodec with
        member this.DecodeWithCid(stream: Stream): Result<DataModelNode * Cid, exn> = result {
            use memoryStream = new MemoryStream()
            stream.CopyTo(memoryStream)
            let! dataModelNode = this.Decode(memoryStream)
            memoryStream.Seek(0, SeekOrigin.Begin) |> ignore
            let multiHash = MultiHash.ComputeHash(memoryStream, "sha2-256")
            let cid = Cid.create "base32" 1 this.CodecName multiHash
            return dataModelNode, cid
        }
