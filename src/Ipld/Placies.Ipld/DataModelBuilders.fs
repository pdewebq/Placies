namespace Placies.Ipld

open Placies


type DataModelListBuilder() =

    member inline _.Yield(elem: DataModelNode): DataModelNode list =
        [ elem ]

    member inline _.YieldFrom(elems: DataModelNode list): DataModelNode list =
        elems

    member inline _.Zero(): DataModelNode list =
        []

    member inline _.Delay(f: unit -> 'a): 'a =
        f ()

    member inline _.Combine(elems1: DataModelNode list, elems2: DataModelNode list): DataModelNode list =
        elems1 @ elems2

    member inline _.For(sequence: 'a seq, body: 'a -> DataModelNode list): DataModelNode list =
        sequence |> Seq.collect body |> Seq.toList

    member inline _.Run(elems: DataModelNode list): DataModelNode =
        DataModelNode.List elems

[<AutoOpen>]
module DataModeListBuilder =
    let dataModelList = DataModelListBuilder()

[<AutoOpen>]
module DataModelListBuilderExtensions =
    [<AutoOpen>]
    module LowPriority =
        type DataModelListBuilder with

            member inline this.Yield(value: bool): DataModelNode list =
                this.Yield(DataModelNode.Boolean value)

            member inline this.Yield(value: int): DataModelNode list =
                this.Yield(DataModelNode.Integer value)

            member inline this.Yield(value: float): DataModelNode list =
                this.Yield(DataModelNode.Float value)

            member inline this.Yield(value: string): DataModelNode list =
                this.Yield(DataModelNode.String value)

            member inline this.Yield(value: Cid): DataModelNode list =
                this.Yield(DataModelNode.Link value)

// ----

type DataModelMapBuilder() =

    member inline _.Yield(_: DataModelNode * DataModelNode as (key, value)): (DataModelNode * DataModelNode) list =
        [ key, value ]

    member inline _.YieldFrom(props: (DataModelNode * DataModelNode) seq): (DataModelNode * DataModelNode) list =
        props |> Seq.toList

    member inline _.Zero(): (DataModelNode * DataModelNode) list =
        []

    member inline _.Delay(f: unit -> 'a): 'a =
        f ()

    member inline _.Combine(props1: (DataModelNode * DataModelNode) list, props2: (DataModelNode * DataModelNode) list): (DataModelNode * DataModelNode) list =
        props1 @ props2

    member inline _.For(sequence: 'a seq, body: 'a -> (DataModelNode * DataModelNode) list): (DataModelNode * DataModelNode) list =
        sequence |> Seq.collect body |> Seq.toList

    member inline _.Run(props: (DataModelNode * DataModelNode) list): DataModelNode =
        DataModelNode.Map (Map.ofList props)

[<AutoOpen>]
module DataModeMapBuilder =
    let dataModelMap = DataModelMapBuilder()

[<AutoOpen>]
module DataModelMapBuilderExtensions =
    [<AutoOpen>]
    module MediumPriority =
        type DataModelMapBuilder with

            member this.Yield(_: string * DataModelNode as (key, value)): (DataModelNode * DataModelNode) list =
                this.Yield((DataModelNode.String key, value))

            member this.YieldFrom(props: (string * DataModelNode) list): (DataModelNode * DataModelNode) list =
                this.YieldFrom(props |> List.map (fun (k, v) -> DataModelNode.String k, v))

    [<AutoOpen>]
    module LowPriority =
        type DataModelMapBuilder with

            member this.Yield(_: string * bool as (key, value)): (DataModelNode * DataModelNode) list =
                this.Yield((key, DataModelNode.Boolean value))

            member this.Yield(_: string * int as (key, value)): (DataModelNode * DataModelNode) list =
                this.Yield((key, DataModelNode.Integer value))

            member this.Yield(_: string * float as (key, value)): (DataModelNode * DataModelNode) list =
                this.Yield((key, DataModelNode.Float value))

            member this.Yield(_: string * string as (key, value)): (DataModelNode * DataModelNode) list =
                this.Yield((key, DataModelNode.String value))

            member this.Yield(_: string * Cid as (key, value)): (DataModelNode * DataModelNode) list =
                this.Yield((key, DataModelNode.Link value))
