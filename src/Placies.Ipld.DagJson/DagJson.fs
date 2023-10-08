namespace Placies.Ipld.DagJson

open System
open System.Collections.Generic
open System.Text.Json
open System.Text.Json.Nodes
open FsToolkit.ErrorHandling
open Placies
open Placies.Ipld

[<AutoOpen>]
module ConvertExtensions =
    type Convert with
        static member ToBase64StringNoPadding(inArray: byte array): string =
            Convert.ToBase64String(inArray).TrimEnd('=')
        static member FromBase64StringNoPadding(s: string): byte array =
            let s =
                match s.Length % 4 with
                | 2 -> s + "=="
                | 3 -> s + "="
                | _ -> s
            Convert.FromBase64String(s)

[<RequireQualifiedAccess>]
module DagJson =

    let rec encode (node: DataModelNode) : JsonNode =
        match node with
        | DataModelNode.Null -> null
        | DataModelNode.Boolean b -> JsonValue.Create(b)
        | DataModelNode.Integer i -> JsonValue.Create(i)
        | DataModelNode.Float f -> JsonValue.Create(f)
        | DataModelNode.String s -> JsonValue.Create(s)
        | DataModelNode.Bytes bytes ->
            JsonObject([
                KeyValuePair(
                    "/",
                    JsonObject([
                        KeyValuePair(
                            "bytes",
                            JsonValue.Create(Convert.ToBase64StringNoPadding(bytes)) :> JsonNode
                        )
                    ]) :> JsonNode
                )
            ])
        | DataModelNode.List list ->
            JsonArray(
                list |> Seq.map encode |> Seq.toArray
            )
        | DataModelNode.Map map ->
            JsonObject(
                map
                |> Map.toSeq
                |> Seq.map ^fun (key, value) ->
                    KeyValuePair(key, encode value)
            )
        | DataModelNode.Link cid ->
            JsonObject([
                KeyValuePair(
                    "/",
                    JsonValue.Create(
                        cid.ShipyardCid.Encode()
                    ) :> JsonNode
                )
            ])

    let rec tryDecode (jsonNode: JsonNode) : Result<DataModelNode, string> =
        match jsonNode with
        | null ->
            Ok DataModelNode.Null
        | :? JsonValue as jsonValue ->
            None
            |> Option.orElseWith ^fun () -> jsonValue.TryGetValue<bool>() |> Option.ofTryByref |> Option.map DataModelNode.Boolean
            |> Option.orElseWith ^fun () -> jsonValue.TryGetValue<int64>() |> Option.ofTryByref |> Option.map DataModelNode.Integer
            |> Option.orElseWith ^fun () -> jsonValue.TryGetValue<float>() |> Option.ofTryByref |> Option.map DataModelNode.Float
            |> Option.orElseWith ^fun () -> jsonValue.TryGetValue<string>() |> Option.ofTryByref |> Option.map DataModelNode.String
            |> Option.map Ok
            |> Option.defaultWith ^fun () -> Error $"Invalid JsonValue: %A{jsonValue}"
        | :? JsonObject as jsonObject ->
            match jsonObject.TryGetPropertyValue("/") |> Option.ofTryByref with
            | Some slashJsonNode ->
                match slashJsonNode with
                | :? JsonValue as slashJsonValue ->
                    result {
                        let! cidStr = slashJsonValue.TryGetValue<string>() |> Option.ofTryByref |> Result.requireSome "Scalar is not a string"
                        let! cid = Cid.tryDecode cidStr |> Result.mapError (fun ex -> $"Invalid CID: {ex}")
                        return DataModelNode.Link cid
                    }
                | :? JsonObject as slashJsonObject ->
                    result {
                        let! bytesJsonNode = slashJsonObject.TryGetPropertyValue("bytes") |> Option.ofTryByref |> Result.requireSome "Object does not contain 'bytes' field"
                        let! bytesJsonValue = bytesJsonNode |> tryUnbox<JsonValue> |> Result.requireSome "Is not JsonValue"
                        let! bytesString = bytesJsonValue.TryGetValue<string>() |> Option.ofTryByref |> Result.requireSome "Is not string"
                        let! bytes = Result.tryWith (fun () -> Convert.FromBase64StringNoPadding(bytesString)) |> Result.mapError (fun ex -> $"Invalid Base64: {ex}")
                        return DataModelNode.Bytes bytes
                    }
                | _ ->
                    Error $"Invalid JsonNode in '/': %A{slashJsonNode}"
            | None ->
                jsonObject
                |> Seq.toList
                |> List.traverseResultM ^fun (KeyValue (key, value)) ->
                    tryDecode value |> Result.map (fun value -> key, value)
                |> Result.map (Map.ofList >> DataModelNode.Map)
        | :? JsonArray as jsonArray ->
            jsonArray
            |> Seq.toList
            |> List.traverseResultM tryDecode
            |> Result.map DataModelNode.List
        | _ ->
            Error $"Invalid JsonNode: %A{jsonNode}"

type DagJsonCodec() =
    static member AddShipyardMulticodec() =
        Ipfs.Registry.Codec.Register("dag-json", 0x0129) |> ignore
    interface ICodec with
        member _.CodecName = "dag-json"

        member this.Encode(writeToStream, dataModelNode) =
            let jsonNode = DagJson.encode dataModelNode
            let jsonWriterOptions = JsonWriterOptions(
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            )
            use utf8JsonWriter = new Utf8JsonWriter(writeToStream, jsonWriterOptions)
            let jsonSerializerOptions = JsonSerializerOptions(JsonSerializerOptions.Default)
            JsonSerializer.Serialize(utf8JsonWriter, jsonNode, jsonSerializerOptions)

        member this.Decode(stream) =
            let jsonNode = JsonSerializer.Deserialize<JsonNode>(stream)
            DagJson.tryDecode jsonNode |> Result.mapError exn
