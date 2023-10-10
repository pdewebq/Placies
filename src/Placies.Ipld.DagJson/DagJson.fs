namespace Placies.Ipld.DagJson

open System.Collections.Generic
open System.Text.Json
open System.Text.Json.Nodes
open FsToolkit.ErrorHandling
open Placies
open Placies.Multiformats
open Placies.Ipld

[<RequireQualifiedAccess>]
module DagJson =

    let rec tryEncode (node: DataModelNode) : Result<JsonNode, string> = result {
        match node with
        | DataModelNode.Null -> return (null: JsonNode)
        | DataModelNode.Boolean b -> return JsonValue.Create(b)
        | DataModelNode.Integer i -> return JsonValue.Create(i)
        | DataModelNode.Float f -> return JsonValue.Create(f)
        | DataModelNode.String s -> return JsonValue.Create(s)
        | DataModelNode.Bytes bytes ->
            return JsonObject([
                KeyValuePair(
                    "/",
                    JsonObject([
                        KeyValuePair(
                            "bytes",
                            JsonValue.Create(MultiBaseInfos.Base64.BaseEncoder.Encode(bytes)) :> JsonNode
                        )
                    ]) :> JsonNode
                )
            ])
        | DataModelNode.List list ->
            let! jsonElems = list |> List.traverseResultM tryEncode
            return JsonArray(jsonElems |> List.toArray)
        | DataModelNode.Map map ->
            let! jsonProps =
                map
                |> Map.toList
                |> List.traverseResultM ^fun (k, v) -> result {
                    let! k = k |> function DataModelNode.String k -> Ok k | _ -> Error "Key can be only String"
                    let! v = tryEncode v
                    return k, v
                }
            return JsonObject(jsonProps |> Seq.map KeyValuePair)
        | DataModelNode.Link cid ->
            return JsonObject([
                KeyValuePair(
                    "/",
                    JsonValue.Create(Cid.encode cid) :> JsonNode
                )
            ])
    }

    let rec tryDecode (multibaseProvider: IMultiBaseProvider) (jsonNode: JsonNode) : Result<DataModelNode, string> =
        let tryDecode = tryDecode multibaseProvider
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
                        let! cid = Cid.tryParse multibaseProvider cidStr |> Result.mapError (fun ex -> $"Invalid CID: {ex}")
                        return DataModelNode.Link cid
                    }
                | :? JsonObject as slashJsonObject ->
                    result {
                        let! bytesJsonNode = slashJsonObject.TryGetPropertyValue("bytes") |> Option.ofTryByref |> Result.requireSome "Object does not contain 'bytes' field"
                        let! bytesJsonValue = bytesJsonNode |> tryUnbox<JsonValue> |> Result.requireSome "Is not JsonValue"
                        let! bytesString = bytesJsonValue.TryGetValue<string>() |> Option.ofTryByref |> Result.requireSome "Is not string"
                        let! bytes = Result.tryWith (fun () -> MultiBaseInfos.Base64.BaseEncoder.Decode(bytesString)) |> Result.mapError (fun ex -> $"Invalid Base64: {ex}")
                        return DataModelNode.Bytes bytes
                    }
                | _ ->
                    Error $"Invalid JsonNode in '/': %A{slashJsonNode}"
            | None ->
                jsonObject
                |> Seq.toList
                |> List.traverseResultM ^fun (KeyValue (key, value)) ->
                    tryDecode value |> Result.map (fun value -> DataModelNode.String key, value)
                |> Result.map (Map.ofList >> DataModelNode.Map)
        | :? JsonArray as jsonArray ->
            jsonArray
            |> Seq.toList
            |> List.traverseResultM tryDecode
            |> Result.map DataModelNode.List
        | _ ->
            Error $"Invalid JsonNode: %A{jsonNode}"

type DagJsonCodec(multibaseProvider: IMultiBaseProvider) =
    interface ICodec with
        member _.CodecInfo = MultiCodecInfos.DagJson

        member this.Encode(writeToStream, dataModelNode) = result {
            let! jsonNode = DagJson.tryEncode dataModelNode |> Result.mapError exn
            let jsonWriterOptions = JsonWriterOptions(
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            )
            use utf8JsonWriter = new Utf8JsonWriter(writeToStream, jsonWriterOptions)
            let jsonSerializerOptions = JsonSerializerOptions(JsonSerializerOptions.Default)
            JsonSerializer.Serialize(utf8JsonWriter, jsonNode, jsonSerializerOptions)
        }

        member this.Decode(stream) = result {
            let jsonNode = JsonSerializer.Deserialize<JsonNode>(stream)
            return! DagJson.tryDecode multibaseProvider jsonNode |> Result.mapError exn
        }
