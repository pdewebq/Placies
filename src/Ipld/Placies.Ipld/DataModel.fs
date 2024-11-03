namespace Placies.Ipld

open Placies

[<RequireQualifiedAccess>]
type DataModeKind =
    | Null
    | Boolean
    | Integer
    | Float
    | String
    | Bytes
    | List
    | Map
    | Link

[<RequireQualifiedAccess>]
type DataModelNode =
    | Null
    | Boolean of bool
    | Integer of int64 // TODO?: varint
    | Float of float
    | String of string
    | Bytes of byte array
    | List of DataModelNode list
    | Map of Map<DataModelNode, DataModelNode>
    | Link of Cid

[<RequireQualifiedAccess>]
module DataModelNode =

    let getKind (dataModeNode: DataModelNode) : DataModeKind =
        match dataModeNode with
        | DataModelNode.Null -> DataModeKind.Null
        | DataModelNode.Boolean _ -> DataModeKind.Boolean
        | DataModelNode.Integer _ -> DataModeKind.Integer
        | DataModelNode.Float _ -> DataModeKind.Float
        | DataModelNode.String _ -> DataModeKind.String
        | DataModelNode.Bytes _ -> DataModeKind.Bytes
        | DataModelNode.List _ -> DataModeKind.List
        | DataModelNode.Map _ -> DataModeKind.Map
        | DataModelNode.Link _ -> DataModeKind.Link

    let tryAsInteger (node: DataModelNode) : int64 option =
        match node with
        | DataModelNode.Integer i -> i |> Some
        | _ -> None

    let tryAsBoolean (node: DataModelNode) : bool option =
        match node with
        | DataModelNode.Boolean b -> b |> Some
        | _ -> None

    let tryAsString (node: DataModelNode) : string option =
        match node with
        | DataModelNode.String str -> str |> Some
        | _ -> None

    let tryAsBytes (node: DataModelNode) : byte array option =
        match node with
        | DataModelNode.Bytes bytes -> bytes |> Some
        | _ -> None

    let tryAsMap (node: DataModelNode) : Map<DataModelNode, DataModelNode> option =
        match node with
        | DataModelNode.Map map -> map |> Some
        | _ -> None

    let tryAsList (node: DataModelNode) : DataModelNode list option =
        match node with
        | DataModelNode.List list -> list |> Some
        | _ -> None

    let tryAsLink (node: DataModelNode) : Cid option =
        match node with
        | DataModelNode.Link cid -> cid |> Some
        | _ -> None

    let tryAsMapAndFindField (key: DataModelNode) (node: DataModelNode) : DataModelNode option =
        node |> tryAsMap |> Option.bind (Map.tryFind key)


type DataModelNode with
    member this.Kind = DataModelNode.getKind this
