namespace Placies.Ipld.DagCbor

open FsToolkit.ErrorHandling
open PeterO.Cbor
open Placies
open Placies.Multiformats
open Placies.Ipld

[<RequireQualifiedAccess>]
module DagCbor =

    let rec encode (dataModelNode: DataModelNode) : CBORObject =
        match dataModelNode with
        | DataModelNode.Null ->
            CBORObject.Null
        | DataModelNode.Boolean b ->
            if b then CBORObject.True else CBORObject.False
        | DataModelNode.Integer i ->
            CBORObject.FromObject(i)
        | DataModelNode.Float f ->
            CBORObject.FromObject(f)
        | DataModelNode.String s ->
            CBORObject.FromObject(s)
        | DataModelNode.Bytes bytes ->
            CBORObject.FromObject(bytes)
        | DataModelNode.List list ->
            let cborObject = CBORObject.NewArray()
            for node in list do
                cborObject.Add(encode node) |> ignore
            cborObject
        | DataModelNode.Map map ->
            let cborObject = CBORObject.NewMap()
            for key, value in map |> Map.toSeq do
                cborObject.Add(key, encode value) |> ignore
            cborObject
        | DataModelNode.Link cid ->
            let bytes = [|
                yield 0x00uy // Multibase prefix
                yield! cid |> Cid.toByteArray
            |]
            CBORObject.FromObjectAndTag(bytes, 42)

    let rec tryDecode (cbor: CBORObject) : Result<DataModelNode, string> = result {
        if cbor.IsNull then
            return DataModelNode.Null
        else
        match cbor.Type with
        | CBORType.Boolean ->
            return DataModelNode.Boolean ^ cbor.AsBoolean()
        | CBORType.Integer ->
            return DataModelNode.Integer ^ cbor.AsInt64Value()
        | CBORType.FloatingPoint ->
            return DataModelNode.Float ^ cbor.AsDoubleValue()
        | CBORType.TextString ->
            return DataModelNode.String ^ cbor.AsString()
        | CBORType.ByteString ->
            if cbor.HasTag(42) then
                let bytes = cbor.GetByteString() |> Array.skip 1
                let cid = Cid.ofByteArray bytes
                return DataModelNode.Link cid
            else
                return DataModelNode.Bytes ^ cbor.GetByteString()
        | CBORType.Array ->
            return!
                cbor.Values
                |> Seq.toList
                |> List.traverseResultM tryDecode
                |> Result.map DataModelNode.List
        | CBORType.Map ->
            return!
                cbor.Entries
                |> Seq.toList
                |> List.traverseResultM ^fun (KeyValue (key, value)) ->
                    tryDecode value |> Result.map (fun v -> key.AsString(), v)
                |> Result.map (Map.ofList >> DataModelNode.Map)
        | _ ->
            return! Error $"Invalid CBOR: %A{cbor}"
    }


type DagCborCodec() =
    interface ICodec with
        member this.CodecInfo = MultiCodecInfos.DagCbor
        member this.Decode(stream) =
            let cbor = CBORObject.Read(stream)
            DagCbor.tryDecode cbor |> Result.mapError exn
        member this.Encode(writeToStream, dataModelNode) =
            let cbor = DagCbor.encode dataModelNode
            CBORObject.Write(cbor, writeToStream, CBOREncodeOptions("float64=true"))
