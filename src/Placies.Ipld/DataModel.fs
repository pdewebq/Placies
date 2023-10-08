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
    | Map of Map<string, DataModelNode> // TODO: Map<DataModelNode, DataModelNode>
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

type DataModelNode with
    member this.Kind = DataModelNode.getKind this
