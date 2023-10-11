namespace Placies.Ipld.DagCbor

open FsToolkit.ErrorHandling
open PeterO.Cbor
open Placies
open Placies.Utils
open Placies.Multiformats
open Placies.Ipld

[<RequireQualifiedAccess>]
module DagCbor =

    let rec tryEncode (dataModelNode: DataModelNode) : Result<CBORObject, string> = result {
        match dataModelNode with
        | DataModelNode.Null ->
            return CBORObject.Null
        | DataModelNode.Boolean b ->
            return if b then CBORObject.True else CBORObject.False
        | DataModelNode.Integer i ->
            return CBORObject.FromObject(i)
        | DataModelNode.Float f ->
            return CBORObject.FromObject(f)
        | DataModelNode.String s ->
            return CBORObject.FromObject(s)
        | DataModelNode.Bytes bytes ->
            return CBORObject.FromObject(bytes)
        | DataModelNode.List list ->
            return!
                list
                |> List.traverseResultM tryEncode
                |> Result.map ^fun list ->
                    let cborObject = CBORObject.NewArray()
                    for cbor in list do
                        cborObject.Add(cbor) |> ignore
                    cborObject
        | DataModelNode.Map map ->
            return!
                map
                |> Map.toList
                |> List.traverseResultM ^fun (k, v) -> result {
                    let! k = k |> function DataModelNode.String k -> Ok k | _ -> Error "Key can be only String"
                    let! v = tryEncode v
                    return k, v
                }
                |> Result.map ^fun elems ->
                    let cborObject = CBORObject.NewMap()
                    for key, value in elems do
                        cborObject.Add(key, value) |> ignore
                    cborObject
        | DataModelNode.Link cid ->
            let bytes = [|
                yield 0x00uy // Multibase prefix
                yield! cid |> Cid.toByteArray
            |]
            return CBORObject.FromObjectAndTag(bytes, 42)
    }

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
                    tryDecode value |> Result.map (fun v -> DataModelNode.String (key.AsString()), v)
                |> Result.map (Map.ofList >> DataModelNode.Map)
        | _ ->
            return! Error $"Invalid CBOR: %A{cbor}"
    }


type DagCborCodec() =
    interface ICodec with
        member this.CodecInfo = MultiCodecInfos.DagCbor

        member this.Decode(stream) = result {
            let cbor = CBORObject.Read(stream)
            return! DagCbor.tryDecode cbor |> Result.mapError exn
        }

        member this.Encode(writeToStream, dataModelNode) = result {
            let! cbor = DagCbor.tryEncode dataModelNode |> Result.mapError exn
            CBORObject.Write(cbor, writeToStream, CBOREncodeOptions("float64=true"))
        }
