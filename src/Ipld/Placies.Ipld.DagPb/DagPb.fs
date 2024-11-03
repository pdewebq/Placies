namespace Placies.Ipld.DagPb

open System
open System.Buffers
open System.IO
open FsToolkit.ErrorHandling
open Placies.Ipld
open Placies.Multiformats

// https://ipld.io/specs/codecs/dag-pb/spec/

[<RequireQualifiedAccess>]
module DagPb =

    let pbLinkToDataModel (pbLink: PBLink) : DataModelNode =
        dataModelMap {
            "Hash", DataModelNode.Link pbLink.Hash
            match pbLink.Name with
            | ValueSome name -> "Name", DataModelNode.String name
            | ValueNone -> ()
            match pbLink.Tsize with
            | ValueSome tsize -> "Tsize", DataModelNode.Integer (int64<uint64> tsize)
            | ValueNone -> ()
        }

    let pbNodeToDataModel (pbNode: PBNode) : DataModelNode =
        dataModelMap {
            "Links", DataModelNode.List [ for pbLink in pbNode.Links -> pbLinkToDataModel pbLink ]
            match pbNode.Data with
            | ValueSome data -> "Data", DataModelNode.Bytes (data.ToArray())
            | ValueNone -> ()
        }

    let parseDataModelToPbLink (dataModelNode: DataModelNode) : Validation<PBLink, _> = validation {
        let! hash = validation {
            let! hash = dataModelNode |> DataModelNode.tryAsMapAndFindField (DataModelNode.String "Hash") |> Result.requireSome "No 'Hash' field"
            let! hash = hash |> DataModelNode.tryAsLink |> Result.requireSome "'Hash' field is not Link"
            return hash
        }
        and! name = validation {
            let name = dataModelNode |> DataModelNode.tryAsMapAndFindField (DataModelNode.String "Name")
            match name with
            | None -> return ValueNone
            | Some name ->
                let! name = name |> DataModelNode.tryAsString |> Result.requireSome "'Name' field is not String"
                return ValueSome name
        }
        and! tsize = validation {
            let tsize = dataModelNode |> DataModelNode.tryAsMapAndFindField (DataModelNode.String "Tsize")
            match tsize with
            | None -> return ValueNone
            | Some tsize ->
                let! tsize = tsize |> DataModelNode.tryAsInteger |> Result.requireSome "'Tszie' field is not Integer"
                return ValueSome tsize
        }
        return {
            Hash = hash
            Name = name
            Tsize = tsize |> ValueOption.map uint64<int64>
        }
    }

    let parseDataModelToPbNode (dataModelNode: DataModelNode) : Validation<PBNode, _> = validation {
        let! links = validation {
            let! links = dataModelNode |> DataModelNode.tryAsMapAndFindField (DataModelNode.String "Links") |> Result.requireSome "No 'Links' field"
            let! links = links |> DataModelNode.tryAsList |> Result.requireSome "'Links' field is not List"
            let! pbLinks =
                links
                |> List.map parseDataModelToPbLink
                |> List.sequenceValidationA
            let pbLinks =
                pbLinks
                |> List.sortBy (fun pbLink ->
                    pbLink.Name // TODO: Sort by bytes, not string
                )
            return pbLinks
        }
        and! data = validation {
            let data = dataModelNode |> DataModelNode.tryAsMapAndFindField (DataModelNode.String "Data")
            match data with
            | None -> return ValueNone
            | Some data ->
                let! data = data |> DataModelNode.tryAsBytes |> Result.requireSome "'Data' field is not Bytes"
                return ValueSome (ReadOnlyMemory(data))
        }
        return {
            Links = links |> List.toArray
            Data = data
        }
    }

type DagPbCodec() =
    interface ICodec with
        member this.CodecInfo = MultiCodecInfos.DagPb

        member this.TryDecodeAsync(stream) = taskResult {
            use memoryStream = new MemoryStream()
            do! stream.CopyToAsync(memoryStream)
            let mutable buffer = ReadOnlyMemory(memoryStream.ToArray())
            let! pbNode = DagPbDecode.decodeNode buffer
            return DagPb.pbNodeToDataModel pbNode
        }

        member this.TryEncodeAsync(writeToStream, dataModelNode) = taskResult {
            let! pbNode =
                DagPb.parseDataModelToPbNode dataModelNode
                |> Result.mapError (fun errs -> AggregateException("Invalid dag-pb data model", errs |> Seq.map exn) :> exn)
            let bufferWriter = ArrayBufferWriter()
            DagPbEncode.writeNode pbNode bufferWriter
            do! writeToStream.WriteAsync(bufferWriter.WrittenMemory)
            return ()
        }
