namespace Placies.Ipld

open System.IO
open Placies.Multiformats

type ICodec =
    abstract CodecInfo: MultiCodecInfo
    abstract Encode: writeToStream: Stream * dataModelNode: DataModelNode -> unit
    abstract Decode: stream: Stream -> Result<DataModelNode, exn>
